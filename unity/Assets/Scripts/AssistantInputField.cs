using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class AssistantInputField : TMP_InputField
{
    private readonly Event keyEvent = new();

    public override void OnUpdateSelected(BaseEventData eventData)
    {
        if (!isFocused || !interactable || readOnly) return;
        bool consumed = false;
        while (Event.PopEvent(keyEvent))
        {
            if (keyEvent.rawType == EventType.KeyDown)
            {
                consumed = true;
                bool finished = ProcessChatKey(keyEvent);
                ForceLabelUpdate();
                textComponent.ForceMeshUpdate();
                if (finished) break;
            }
            else if ((keyEvent.rawType == EventType.ValidateCommand || keyEvent.rawType == EventType.ExecuteCommand) &&
                keyEvent.commandName == "SelectAll")
            {
                SelectAll();
                consumed = true;
            }
        }
        if (consumed) eventData.Use();
    }

    private bool ProcessChatKey(Event key)
    {
        if (key.keyCode == KeyCode.Return || key.keyCode == KeyCode.KeypadEnter)
        {
            var bridge = GetComponent<DesktopTextInputBridge>();
            if (bridge != null) bridge.SuppressReturnFallbackThisFrame();
            if (bridge != null && bridge.NativeCommittedThisFrame)
            {
                return false;
            }
#if ENABLE_LEGACY_INPUT_MANAGER
            if (!string.IsNullOrEmpty(Input.compositionString)) return false;
#endif
            if (key.shift)
            {
                // TMP turns Shift+Enter into a vertical-tab control character.
                // Insert a normal newline while retaining the field's focus.
                KeyPressed(new Event { type = EventType.KeyDown, character = '\n', keyCode = KeyCode.None });
                return false;
            }
            SendOnSubmit();
            return true;
        }
        if (KeyPressed(key) != EditState.Finish) return false;
        if (!wasCanceled) SendOnSubmit();
        DeactivateInputField();
        return true;
    }
}
