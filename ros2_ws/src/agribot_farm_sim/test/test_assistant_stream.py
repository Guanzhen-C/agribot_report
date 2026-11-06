import io
import json

import pytest

from agribot_farm_sim.assistant_stream import StreamProtocolError, read_completion, sse_events, visible_prefix


def events(deltas, finish='stop', done=True):
    data = [': keepalive\r\n\r\n']
    for delta in deltas:
        data.append('data: ' + json.dumps(dict(choices=[dict(delta=delta)])) + '\r\n\r\n')
    data.append('data: ' + json.dumps(dict(choices=[dict(delta={}, finish_reason=finish)])) + '\n\n')
    if done:
        data.append('data: [DONE]\n\n')
    return io.BytesIO(''.join(data).encode())


@pytest.mark.parametrize('ensure_ascii', [False, True])
def test_every_split_preserves_chinese_escapes_and_surrogate_pairs(ensure_ascii):
    answer = '你好，农机！\n"引号"和\\路径🌾'
    raw = json.dumps(dict(answer=answer, recommendations=[], memory='私有备忘'), ensure_ascii=ensure_ascii)
    previews = [visible_prefix(raw[:end]) for end in range(len(raw) + 1)]
    assert all(answer.startswith(text) for text in previews)
    assert previews[-1] == answer
    assert any(text == '你' for text in previews)


def test_only_top_level_user_facing_strings_are_shown():
    assert visible_prefix('{"memory":"secret", "recommendations":[{"answer":"hidden"}], "answer":"正文') == '正文'
    assert visible_prefix('{"answer":"", "clarification":"请选择场景') == '请选择场景'
    assert visible_prefix('{"answer":"正文","clarification":"追问') == '正文'
    assert visible_prefix('{"answer":') == ''


def test_stream_previews_before_completion_not_a_typewriter():
    observed = []
    source = events([dict(role='assistant'), dict(content='{"answer":"你'),
                     dict(content='好","recommendations":[]}')])
    result = read_completion(source, lambda text: observed.append((text, source.tell())))
    assert result['answer'] == '你好'
    assert [text for text, _ in observed] == ['你', '你好']
    assert observed[0][1] < observed[1][1] < len(source.getvalue())


def test_fragmented_tool_call_is_reassembled_without_leaking_arguments():
    result = read_completion(events([
        dict(tool_calls=[dict(index=0, id='call_1', type='function', function=dict(name='list_scenes', arguments='{'))]),
        dict(tool_calls=[dict(index=0, function=dict(arguments='}'))]),
    ], finish='tool_calls'), lambda _: pytest.fail('Tool arguments must not be displayed'))
    tool = result['_tool_message']['tool_calls'][0]
    assert tool == dict(id='call_1', type='function', function=dict(name='list_scenes', arguments='{}'))


@pytest.mark.parametrize('finish,done', [('length', True), ('stop', False), (None, True)])
def test_truncated_or_incomplete_stream_is_not_success(finish, done):
    with pytest.raises(StreamProtocolError):
        read_completion(events([dict(content='{"answer":"你好","recommendations":[]}')], finish, done))


def test_invalid_json_and_server_error_are_rejected():
    with pytest.raises(json.JSONDecodeError):
        read_completion(events([dict(content='not JSON')]))
    with pytest.raises(StreamProtocolError):
        read_completion(io.BytesIO(b'data: {"error":{"message":"private"}}\n\n'))


def test_multiline_sse_and_bounds():
    assert list(sse_events(io.BytesIO(b': ping\ndata: one\ndata: two\n\n'))) == ['one\ntwo']
    with pytest.raises(StreamProtocolError):
        list(sse_events(io.BytesIO(b'data: ' + b'x' * 512_001 + b'\n')))


def test_transport_actually_requests_stream_and_publishes_only_text(monkeypatch):
    from agribot_farm_sim import natural_recommendation as service
    monkeypatch.setattr(service, 'configuration', lambda: ('private-test-key', 'https://api.deepseek.com', 'deepseek-flash'))
    def open_response(request, timeout):
        body = json.loads(request.data)
        assert body['stream'] is True
        assert body['model'] == 'deepseek-flash'
        return events([dict(content='{"answer":"你'), dict(content='好","recommendations":[]}')])
    monkeypatch.setattr(service, 'model_urlopen', open_response)
    updates = []
    reply = service.request_model_reply(dict(stream=True), updates.append)
    assert reply['answer'] == '你好'
    assert updates == [dict(type='text', text='你'), dict(type='text', text='你好')]


def test_correction_resets_preview_and_only_valid_final_result_is_returned():
    from agribot_farm_sim.natural_recommendation import recommend
    updates, calls = [], []
    def call(payload):
        calls.append(payload)
        assert payload['stream'] is True
        return dict(answer='正文', recommendations='invalid' if len(calls)==1 else [])
    result = recommend('你好', [], call=call, knowledge=dict(choices=[]), on_progress=updates.append)
    assert result['answer'] == '正文'
    assert len(calls) == 2
    assert [event['type'] for event in updates] == ['reset', 'reset']
    assert '重新生成' in updates[1]['text']
