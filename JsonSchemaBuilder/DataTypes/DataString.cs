using System;
using System.Text.RegularExpressions;

namespace DevelApp.JsonSchemaBuilder.DataTypes
{
    /// <summary>
    /// DataString data type that wraps string with included handling of the Json Schema
    /// string limitations (minLength, maxLength, pattern), following the same pattern as Email.
    /// A DataString that has been constructed is known to satisfy its limitations, so the
    /// limitations do not have to be rechecked wherever the value is used.
    /// </summary>
    public class DataString
    {
        private readonly string _value;

        /// <summary>
        /// Creates a DataString from a string and validates the value against the limitations supplied
        /// </summary>
        /// <param name="value">String value</param>
        /// <param name="minLength">Minimum length limitation, 0 means no limitation</param>
        /// <param name="maxLength">Maximum length limitation, null means no limitation</param>
        /// <param name="pattern">Regex pattern limitation, null means no limitation</param>
        public DataString(string value, uint minLength = 0, uint? maxLength = null, string pattern = null)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            if (value.Length < minLength)
            {
                throw new ArgumentException($"The value ({value}) is shorter than the minLength ({minLength}) supplied", nameof(value));
            }
            if (maxLength.HasValue && value.Length > maxLength.Value)
            {
                throw new ArgumentException($"The value ({value}) is longer than the maxLength ({maxLength}) supplied", nameof(value));
            }
            if (!string.IsNullOrWhiteSpace(pattern))
            {
                if (!Regex.IsMatch(value, pattern))
                {
                    throw new ArgumentException($"The value ({value}) does not match the pattern ({pattern}) supplied", nameof(value));
                }
            }
            _value = value;
            MinLength = minLength;
            MaxLength = maxLength;
            Pattern = pattern;
        }

        /// <summary>
        /// Gets the underlying string value
        /// </summary>
        public string Value => _value;

        /// <summary>
        /// Gets the minimum length limitation, 0 means no limitation
        /// </summary>
        public uint MinLength { get; }

        /// <summary>
        /// Gets the maximum length limitation, null means no limitation
        /// </summary>
        public uint? MaxLength { get; }

        /// <summary>
        /// Gets the regex pattern limitation, null means no limitation
        /// </summary>
        public string Pattern { get; }

        /// <summary>
        /// Gets whether any limitation is attached to this DataString
        /// </summary>
        public bool HasLimitations
        {
            get
            {
                return MinLength > 0 || MaxLength.HasValue || !string.IsNullOrEmpty(Pattern);
            }
        }

        /// <summary>
        /// Gets the length of the value
        /// </summary>
        public int Length => _value?.Length ?? 0;

        /// <summary>
        /// Gets whether the value matches the pattern limitation. Always true when no pattern is supplied
        /// </summary>
        public bool IsPatternMatched
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Pattern) || _value == null)
                {
                    return true;
                }
                return Regex.IsMatch(_value, Pattern);
            }
        }

        /// <summary>
        /// Implicit conversion from string to DataString without limitations.
        /// Null converts to null so nullable members behave like string
        /// </summary>
        public static implicit operator DataString(string value)
        {
            return value == null ? null : new DataString(value);
        }

        /// <summary>
        /// Implicit conversion from DataString to string.
        /// Null converts to null so nullable members behave like string
        /// </summary>
        public static implicit operator string(DataString dataString)
        {
            return dataString?._value;
        }

        /// <summary>
        /// Returns the value as a string
        /// </summary>
        public override string ToString()
        {
            return _value ?? string.Empty;
        }

        /// <summary>
        /// Compares two DataString instances for equality. The comparison is ordinal
        /// since the value is an ordinary string, not a case insensitive format like email
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj is DataString dataString)
            {
                // Handle null cases
                if (_value == null && dataString._value == null)
                    return true;
                if (_value == null || dataString._value == null)
                    return false;

                return string.Equals(_value, dataString._value, StringComparison.Ordinal);
            }
            if (obj is string str)
            {
                return string.Equals(_value, str, StringComparison.Ordinal);
            }
            return false;
        }

        /// <summary>
        /// Gets the hash code for the value
        /// </summary>
        public override int GetHashCode()
        {
            return _value?.GetHashCode() ?? 0;
        }
    }
}
