using System;

namespace DevelApp.JsonSchemaBuilder.DataTypes
{
    /// <summary>
    /// Guid data type that wraps System.Guid with implicit conversions
    /// </summary>
    public class Guid
    {
        private readonly System.Guid _guid;

        /// <summary>
        /// Creates a Guid from a string
        /// </summary>
        /// <param name="guidString">Guid string</param>
        public Guid(string guidString)
        {
            if (string.IsNullOrWhiteSpace(guidString))
            {
                throw new ArgumentNullException(nameof(guidString));
            }
            _guid = System.Guid.Parse(guidString);
        }

        /// <summary>
        /// Creates a Guid from a System.Guid
        /// </summary>
        /// <param name="guid">System.Guid instance</param>
        public Guid(System.Guid guid)
        {
            _guid = guid;
        }

        /// <summary>
        /// Gets the underlying System.Guid
        /// </summary>
        public System.Guid SystemGuid => _guid;

        /// <summary>
        /// Creates a new random Guid
        /// </summary>
        public static Guid NewGuid()
        {
            return new Guid(System.Guid.NewGuid());
        }

        /// <summary>
        /// Implicit conversion from string to Guid
        /// </summary>
        public static implicit operator Guid(string guidString)
        {
            return guidString == null ? null : new Guid(guidString);
        }

        /// <summary>
        /// Implicit conversion from Guid to string
        /// </summary>
        public static implicit operator string(Guid guid)
        {
            return guid?._guid.ToString();
        }

        /// <summary>
        /// Implicit conversion from System.Guid to Guid
        /// </summary>
        public static implicit operator Guid(System.Guid systemGuid)
        {
            return new Guid(systemGuid);
        }

        /// <summary>
        /// Implicit conversion from Guid to System.Guid
        /// </summary>
        public static implicit operator System.Guid(Guid guid)
        {
            return guid?._guid ?? System.Guid.Empty;
        }

        /// <summary>
        /// Returns the Guid as a string
        /// </summary>
        public override string ToString()
        {
            return _guid.ToString();
        }

        /// <summary>
        /// Returns the Guid as a string with the specified format
        /// </summary>
        public string ToString(string format)
        {
            return _guid.ToString(format);
        }

        /// <summary>
        /// Compares two Guid instances for equality
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj is Guid guid)
            {
                return _guid.Equals(guid._guid);
            }
            if (obj is string str)
            {
                if (System.Guid.TryParse(str, out System.Guid parsed))
                {
                    return _guid.Equals(parsed);
                }
            }
            if (obj is System.Guid sysGuid)
            {
                return _guid.Equals(sysGuid);
            }
            return false;
        }

        /// <summary>
        /// Gets the hash code for the Guid
        /// </summary>
        public override int GetHashCode()
        {
            return _guid.GetHashCode();
        }
    }
}
