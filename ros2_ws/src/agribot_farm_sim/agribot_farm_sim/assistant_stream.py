"""Bounded SSE decoding and plain-text previews of the assistant JSON envelope."""
import json


class StreamProtocolError(ValueError):
    pass


def sse_events(response):
    data, total = [], 0
    while True:
        line = response.readline(512_001)
        total += len(line)
        if len(line) > 512_000 or total > 8_000_000:
            raise StreamProtocolError('模型流式内容过大。')
        if not line:
            if data:
                yield '\n'.join(data)
            return
        try:
            line = line.decode('utf-8').rstrip('\r\n')
        except UnicodeDecodeError:
            raise StreamProtocolError('模型流式编码无效。') from None
        if not line:
            if data:
                yield '\n'.join(data)
                data.clear()
        elif line.startswith('data:'):
            data.append(line[5:].removeprefix(' '))


def _string_prefix(text, start):
    """Decode only complete JSON characters, including split surrogate pairs."""
    output, pos = [], start + 1
    while pos < len(text):
        char = text[pos]
        if char == '"':
            return ''.join(output), pos + 1
        if char == '\\':
            if pos + 1 >= len(text):
                break
            end = pos + (6 if text[pos + 1] == 'u' else 2)
            if end > len(text):
                break
            value = json.loads('"' + text[pos:end] + '"')
            if len(value) == 1 and 0xD800 <= ord(value) <= 0xDBFF:
                if end + 6 > len(text):
                    break
                value = json.loads('"' + text[pos:end + 6] + '"')
                if len(value) != 1 or 0xD800 <= ord(value) <= 0xDFFF:
                    raise StreamProtocolError('模型流式字符无效。')
                end += 6
            if any(0xD800 <= ord(c) <= 0xDFFF for c in value):
                raise StreamProtocolError('模型流式字符无效。')
            output.append(value)
            pos = end
        else:
            if ord(char) < 32:
                raise StreamProtocolError('模型流式字符串无效。')
            output.append(char)
            pos += 1
    return ''.join(output), None


def visible_prefix(text):
    """Read top-level answer (or clarification), never raw JSON/tool arguments."""
    decoder, pos, answer = json.JSONDecoder(), 0, ''
    text = text.lstrip()
    if not text.startswith('{'):
        return ''
    pos = 1
    try:
        while pos < len(text):
            while pos < len(text) and text[pos] in ' \r\n\t,':
                pos += 1
            key, pos = decoder.raw_decode(text, pos)
            while pos < len(text) and text[pos].isspace():
                pos += 1
            if pos >= len(text) or text[pos] != ':':
                return answer
            pos += 1
            while pos < len(text) and text[pos].isspace():
                pos += 1
            if key in ('answer', 'clarification') and pos < len(text) and text[pos] == '"':
                value, end = _string_prefix(text, pos)
                if key == 'answer' or not answer:
                    answer = value
                if answer or end is None:
                    return answer
                pos = end
            else:
                _, pos = decoder.raw_decode(text, pos)
    except json.JSONDecodeError:
        pass
    return answer


def read_completion(response, on_text=None):
    content, tools, finish, done, previous = '', {}, None, False, ''
    for event in sse_events(response):
        if event == '[DONE]':
            done = True
            break
        chunk = json.loads(event)
        if chunk.get('error'):
            raise StreamProtocolError('模型流式请求未正常完成。')
        choices = chunk.get('choices', [])
        if not choices:
            continue
        choice = choices[0]
        delta = choice.get('delta') or {}
        content += delta.get('content') or ''
        for part in delta.get('tool_calls') or []:
            index = part['index']
            if type(index) is not int or index != 0:
                raise StreamProtocolError('每轮只允许一次场景目录查询。')
            target = tools.setdefault(index, dict(id='', type='function', function=dict(name='', arguments='')))
            for key in ('id', 'type'):
                if part.get(key):
                    target[key] = part[key]
            for key in ('name', 'arguments'):
                target['function'][key] += (part.get('function') or {}).get(key) or ''
        if len(content) + len(json.dumps(tools)) > 512_000:
            raise StreamProtocolError('模型返回内容过大。')
        # Tool-call content is never an actionable or final assistant answer.
        if on_text and not tools:
            preview = visible_prefix(content)
            if preview and preview != previous:
                on_text(preview)
                previous = preview
        if choice.get('finish_reason') is not None:
            finish = choice['finish_reason']
    if not done or finish not in ('stop', 'tool_calls'):
        raise StreamProtocolError('模型流式输出未完整结束，请重试。')
    if tools and finish in ('stop', 'tool_calls'):
        return {'_tool_message': dict(role='assistant', content=content, tool_calls=list(tools.values()))}
    if finish != 'stop':
        raise StreamProtocolError('模型工具调用未完整结束。')
    return json.loads(content)
