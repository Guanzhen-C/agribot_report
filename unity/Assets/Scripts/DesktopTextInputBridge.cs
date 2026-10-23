using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// Pump native commits before EventSystem dispatches a Send click, then complete
// ordinary key events after UI Update without duplicating TMP's own edits.
[DefaultExecutionOrder(-1000)]
[RequireComponent(typeof(TMP_InputField))]
public sealed class DesktopTextInputBridge : MonoBehaviour
{
    private TMP_InputField field;
    private Keyboard keyboard;
    private readonly StringBuilder pending = new();
    private readonly StringBuilder handled = new();
    private string previousText;
    private bool insertingFallback;
    private int nativeCommitFrame = -1;
    private int suppressReturnFrame = -1;
    internal bool NativeCommittedThisFrame => nativeCommitFrame == Time.frameCount;
    internal void SuppressReturnFallbackThisFrame() => suppressReturnFrame = Time.frameCount;

    private void OnEnable()
    {
        field = GetComponent<TMP_InputField>();
        previousText = field.text;
        field.onValueChanged.AddListener(RecordEdit);
        field.onSelect.AddListener(SelectNativeInput);
        field.onDeselect.AddListener(DeselectNativeInput);
        BindKeyboard();
    }

    private void SelectNativeInput(string _) => LinuxImeCommitBridge.SetActive(Application.isFocused);
    private void DeselectNativeInput(string _) => LinuxImeCommitBridge.SetActive(false);
    private void OnApplicationFocus(bool focused) => LinuxImeCommitBridge.SetActive(focused && field != null && field.isFocused);

    private void Update()
    {
        bool focused = field.isFocused && field.interactable && !field.readOnly && Application.isFocused;
        LinuxImeCommitBridge.SetActive(focused);
        if (!focused) return;
        string commit;
        while ((commit = LinuxImeCommitBridge.Pop()) != null) ApplyNativeCommit(commit);
    }

    private void ApplyNativeCommit(string text)
    {
        nativeCommitFrame = Time.frameCount;
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var printable = new StringBuilder();
        foreach (char c in text)
            if (!char.IsControl(c) || field.multiLine && (c == '\n' || c == '\t')) printable.Append(c);
        text = printable.ToString();
        if (text.Length == 0) return;
        int start = Mathf.Clamp(Mathf.Min(field.selectionStringAnchorPosition, field.selectionStringFocusPosition), 0, field.text.Length);
        int end = Mathf.Clamp(Mathf.Max(field.selectionStringAnchorPosition, field.selectionStringFocusPosition), start, field.text.Length);
        int room = field.characterLimit > 0 ? Mathf.Max(0, field.characterLimit - field.text.Length + end - start) : text.Length;
        if (text.Length > room)
        {
            if (room > 0 && char.IsHighSurrogate(text[room - 1])) --room;
            text = text.Substring(0, room);
        }
        if (text.Length == 0) return;
        insertingFallback = true;
        try
        {
            field.text = field.text.Substring(0, start) + text + field.text.Substring(end);
            field.stringPosition = start + text.Length;
            field.selectionStringAnchorPosition = field.selectionStringFocusPosition = field.stringPosition;
            field.ForceLabelUpdate();
            field.textComponent.ForceMeshUpdate();
            if (System.Environment.GetEnvironmentVariable("AGRIBOT_IME_DIAGNOSTICS") == "1")
                Debug.Log("AGRIBOT_IME_FIELD_COMMIT chars=" + text.Length + " total=" + field.text.Length);
        }
        finally { insertingFallback = false; }
    }

    private void RecordEdit(string value)
    {
        if (!insertingFallback)
        {
            int prefix = 0, suffix = 0;
            while (prefix < previousText.Length && prefix < value.Length && previousText[prefix] == value[prefix]) prefix++;
            while (suffix < previousText.Length - prefix && suffix < value.Length - prefix &&
                   previousText[previousText.Length - suffix - 1] == value[value.Length - suffix - 1]) suffix++;
            handled.Append(value, prefix, value.Length - prefix - suffix);
        }
        previousText = value;
    }

    private void BindKeyboard()
    {
        if (keyboard == Keyboard.current) return;
        if (keyboard != null) keyboard.onTextInput -= ReceiveText;
        keyboard = Keyboard.current;
        if (keyboard != null) keyboard.onTextInput += ReceiveText;
    }

    private void ReceiveText(char value)
    {
        if (!field.isFocused || !field.interactable || field.readOnly ||
            char.IsControl(value) && value != '\n' && value != '\r' && value != '\t') return;
        if (keyboard != null && ((keyboard.ctrlKey.isPressed && !keyboard.altKey.isPressed) ||
            keyboard.leftMetaKey.isPressed || keyboard.rightMetaKey.isPressed)) return;
        pending.Append(value);
    }

    private void LateUpdate()
    {
        BindKeyboard();
        string text = pending.ToString();
        if (suppressReturnFrame == Time.frameCount) text = text.Replace("\r", "").Replace("\n", "");
        string consumed = handled.ToString();
        pending.Clear();
        handled.Clear();
        if (text.Length == 0 || !field.isFocused || !field.interactable || field.readOnly) return;
        // Linux IMEs may deliver the whole commit to Input System but only its
        // first character to TMP. Deduplicate the consumed prefix, not the batch.
        if (!text.StartsWith(consumed, System.StringComparison.Ordinal)) return;
        insertingFallback = true;
        try
        {
            for (int index = consumed.Length; index < text.Length; index++)
                field.ProcessEvent(new Event { type = EventType.KeyDown, character = text[index], keyCode = KeyCode.None });
            field.ForceLabelUpdate();
            field.textComponent.ForceMeshUpdate();
        }
        finally { insertingFallback = false; }
    }

    private void OnDisable()
    {
        LinuxImeCommitBridge.SetActive(false);
        if (field != null)
        {
            field.onValueChanged.RemoveListener(RecordEdit);
            field.onSelect.RemoveListener(SelectNativeInput);
            field.onDeselect.RemoveListener(DeselectNativeInput);
        }
        if (keyboard != null) keyboard.onTextInput -= ReceiveText;
        keyboard = null;
        pending.Clear();
        handled.Clear();
    }
}
