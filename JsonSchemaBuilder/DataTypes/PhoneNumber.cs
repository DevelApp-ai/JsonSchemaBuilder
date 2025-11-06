using System;

namespace DevelApp.JsonSchemaBuilder.DataTypes
{
    /// <summary>
    /// PhoneNumber data type with validation and formatting support
    /// </summary>
    public class PhoneNumber
    {
        private readonly string _phoneNumber;

        /// <summary>
        /// Creates a PhoneNumber from a string
        /// </summary>
        /// <param name="phoneNumber">Phone number string</param>
        public PhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                throw new ArgumentNullException(nameof(phoneNumber));
            }
            _phoneNumber = phoneNumber;
        }

        /// <summary>
        /// Gets the phone number value
        /// </summary>
        public string Value => _phoneNumber;

        /// <summary>
        /// Gets the phone number with only digits
        /// </summary>
        public string DigitsOnly
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_phoneNumber))
                    return string.Empty;
                
                return System.Text.RegularExpressions.Regex.Replace(_phoneNumber, @"[^\d]", "");
            }
        }

        /// <summary>
        /// Implicit conversion from string to PhoneNumber
        /// </summary>
        public static implicit operator PhoneNumber(string phoneNumber)
        {
            return phoneNumber == null ? null : new PhoneNumber(phoneNumber);
        }

        /// <summary>
        /// Implicit conversion from PhoneNumber to string
        /// </summary>
        public static implicit operator string(PhoneNumber phoneNumber)
        {
            return phoneNumber?._phoneNumber;
        }

        /// <summary>
        /// Returns the phone number as a string
        /// </summary>
        public override string ToString()
        {
            return _phoneNumber ?? string.Empty;
        }

        /// <summary>
        /// Compares two PhoneNumber instances for equality
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj is PhoneNumber phone)
            {
                // Handle null cases
                if (_phoneNumber == null && phone._phoneNumber == null)
                    return true;
                if (_phoneNumber == null || phone._phoneNumber == null)
                    return false;
                    
                // Compare digits only for equality
                return DigitsOnly.Equals(phone.DigitsOnly);
            }
            if (obj is string str)
            {
                if (string.IsNullOrWhiteSpace(str))
                    return string.IsNullOrWhiteSpace(_phoneNumber);
                    
                var strDigits = System.Text.RegularExpressions.Regex.Replace(str, @"[^\d]", "");
                return DigitsOnly.Equals(strDigits);
            }
            return false;
        }

        /// <summary>
        /// Gets the hash code for the phone number
        /// </summary>
        public override int GetHashCode()
        {
            return DigitsOnly?.GetHashCode() ?? 0;
        }
    }
}
