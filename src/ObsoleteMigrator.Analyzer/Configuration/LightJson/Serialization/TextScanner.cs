#nullable disable

// ReSharper disable once CheckNamespace
namespace LightJson.Serialization;

using System.IO;
using ErrorType = JsonParseException.ErrorType;

/// <summary>
/// Represents a text scanner that reads one character at a time.
/// </summary>
internal sealed class TextScanner
{
    private readonly TextReader _reader;
    private TextPosition _position;

    /// <summary>
    /// Initializes a new instance of the <see cref="TextScanner"/> class.
    /// </summary>
    /// <param name="reader">The TextReader to read the text.</param>
    public TextScanner(TextReader reader)
    {
        _reader = reader;
    }

    /// <summary>
    /// Gets the position of the scanner within the text.
    /// </summary>
    /// <value>The position of the scanner within the text.</value>
    public TextPosition Position
    {
        get
        {
            return _position;
        }
    }

    /// <summary>
    /// Reads the next character in the stream without changing the current position.
    /// </summary>
    /// <returns>The next character in the stream.</returns>
    public char Peek()
        => (char)Peek(throwAtEndOfFile: true);

    /// <summary>
    /// Reads the next character in the stream without changing the current position.
    /// </summary>
    /// <param name="throwAtEndOfFile"><see langword="true"/> to throw an exception if the end of the file is
    /// reached; otherwise, <see langword="false"/>.</param>
    /// <returns>The next character in the stream, or -1 if the end of the file is reached with
    /// <paramref name="throwAtEndOfFile"/> set to <see langword="false"/>.</returns>
    public int Peek(bool throwAtEndOfFile)
    {
        var next = _reader.Peek();

        if (next == -1 && throwAtEndOfFile)
        {
            throw new JsonParseException(
                ErrorType.IncompleteMessage,
                _position);
        }
        else
        {
            return next;
        }
    }

    /// <summary>
    /// Reads the next character in the stream, advancing the text position.
    /// </summary>
    /// <returns>The next character in the stream.</returns>
    public char Read()
    {
        var next = _reader.Read();

        if (next == -1)
        {
            throw new JsonParseException(
                ErrorType.IncompleteMessage,
                _position);
        }
        else
        {
            if (next == '\n')
            {
                _position.Line += 1;
                _position.Column = 0;
            }
            else
            {
                _position.Column += 1;
            }

            return (char)next;
        }
    }

    /// <summary>
    /// Advances the scanner to next non-whitespace character.
    /// </summary>
    public void SkipWhitespace()
    {
        while (true)
        {
            char next = Peek();
            if (char.IsWhiteSpace(next))
            {
                Read();
                continue;
            }
            else if (next == '/')
            {
                SkipComment();
                continue;
            }
            else
            {
                break;
            }
        }
    }

    /// <summary>
    /// Verifies that the given character matches the next character in the stream.
    /// If the characters do not match, an exception will be thrown.
    /// </summary>
    /// <param name="next">The expected character.</param>
    public void Assert(char next)
    {
        var errorPosition = _position;
        if (Read() != next)
        {
            throw new JsonParseException(
                string.Format("Parser expected '{0}'", next),
                ErrorType.InvalidOrUnexpectedCharacter,
                errorPosition);
        }
    }

    /// <summary>
    /// Verifies that the given string matches the next characters in the stream.
    /// If the strings do not match, an exception will be thrown.
    /// </summary>
    /// <param name="next">The expected string.</param>
    public void Assert(string next)
    {
        for (var i = 0; i < next.Length; i += 1)
        {
            Assert(next[i]);
        }
    }

    private void SkipComment()
    {
        // First character is the first slash
        Read();
        switch (Peek())
        {
            case '/':
                SkipLineComment();
                return;

            case '*':
                SkipBlockComment();
                return;

            default:
                throw new JsonParseException(
                    string.Format("Parser expected '{0}'", Peek()),
                    ErrorType.InvalidOrUnexpectedCharacter,
                    _position);
        }
    }

    private void SkipLineComment()
    {
        // First character is the second '/' of the opening '//'
        Read();

        while (true)
        {
            switch (_reader.Peek())
            {
                case '\n':
                    // Reached the end of the line
                    Read();
                    return;

                case -1:
                    // Reached the end of the file
                    return;

                default:
                    Read();
                    continue;
            }
        }
    }

    private void SkipBlockComment()
    {
        // First character is the '*' of the opening '/*'
        Read();

        bool foundStar = false;
        while (true)
        {
            switch (_reader.Peek())
            {
                case '*':
                    Read();
                    foundStar = true;
                    continue;

                case '/':
                    Read();
                    if (foundStar)
                    {
                        return;
                    }
                    else
                    {
                        foundStar = false;
                        continue;
                    }

                case -1:
                    // Reached the end of the file
                    return;

                default:
                    Read();
                    foundStar = false;
                    continue;
            }
        }
    }
}