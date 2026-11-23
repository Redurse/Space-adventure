using Anabiosis.Shared.Model;

namespace Anabiosis.Client;

// Typing into a wiring part's text setting (the regular expression's pattern, the terminal's message): clicking the text row in the connection
// panel takes the keyboard the way the chat box does (_chatFocused keeps the game keys quiet, OnChatTextInput hands the characters here), Enter
// sends the text to the server (ClientCommand.ComponentText), Escape drops it.
public partial class Game1
{
    private bool _partTextMode;
    private string _partTextComponentId = "";
    private string _partTextBuffer = "";
    private (string ComponentId, string Text)? _pendingComponentText;

    private void BeginPartText(string componentId, string current)
    {
        _partTextMode = true;
        _partTextComponentId = componentId;
        _partTextBuffer = current;
        _chatFocused = true;
        _chatJustOpenedThisFrame = false;
        SyncPartTextToPanel();
    }

    private void EndPartText()
    {
        _partTextMode = false;
        _partTextComponentId = "";
        _partTextBuffer = "";
        _chatFocused = false;
        SyncPartTextToPanel();
    }

    // The connection panel draws the text as it is typed.
    private void SyncPartTextToPanel()
    {
        _connectionsPanel.EditingComponentId = _partTextMode ? _partTextComponentId : null;
        _connectionsPanel.EditingText = _partTextBuffer;
    }

    private void HandlePartTextChar(char character)
    {
        if (character == '\b')
        {
            if (_partTextBuffer.Length > 0)
                _partTextBuffer = _partTextBuffer[..^1];
        }
        else if (character == '\r')
        {
            _pendingComponentText = (_partTextComponentId, _partTextBuffer);
            EndPartText();
            return;
        }
        else if (!char.IsControl(character) && _partTextBuffer.Length < ComponentSettings.MaxTextLength)
            _partTextBuffer += character;
        SyncPartTextToPanel();
    }
}
