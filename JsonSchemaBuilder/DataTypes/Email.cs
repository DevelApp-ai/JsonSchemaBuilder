using System;
using System.Net.Mail;

namespace DevelApp.JsonSchemaBuilder.DataTypes
{
    /// <summary>
    /// Email data type that wraps System.Net.Mail.MailAddress with implicit conversions
    /// </summary>
    public class Email
    {
        private readonly MailAddress _mailAddress;

        /// <summary>
        /// Creates an Email from a string
        /// </summary>
        /// <param name="emailAddress">Email address string</param>
        public Email(string emailAddress)
        {
            if (string.IsNullOrWhiteSpace(emailAddress))
            {
                throw new ArgumentNullException(nameof(emailAddress));
            }
            _mailAddress = new MailAddress(emailAddress);
        }

        /// <summary>
        /// Creates an Email from a MailAddress
        /// </summary>
        /// <param name="mailAddress">MailAddress instance</param>
        public Email(MailAddress mailAddress)
        {
            _mailAddress = mailAddress ?? throw new ArgumentNullException(nameof(mailAddress));
        }

        /// <summary>
        /// Gets the underlying MailAddress
        /// </summary>
        public MailAddress MailAddress => _mailAddress;

        /// <summary>
        /// Gets the email address
        /// </summary>
        public string Address => _mailAddress?.Address;

        /// <summary>
        /// Gets the display name
        /// </summary>
        public string DisplayName => _mailAddress?.DisplayName;

        /// <summary>
        /// Gets the host part of the email address
        /// </summary>
        public string Host => _mailAddress?.Host;

        /// <summary>
        /// Gets the user part of the email address
        /// </summary>
        public string User => _mailAddress?.User;

        /// <summary>
        /// Implicit conversion from string to Email
        /// </summary>
        public static implicit operator Email(string emailAddress)
        {
            return emailAddress == null ? null : new Email(emailAddress);
        }

        /// <summary>
        /// Implicit conversion from Email to string
        /// </summary>
        public static implicit operator string(Email email)
        {
            return email?._mailAddress?.Address;
        }

        /// <summary>
        /// Implicit conversion from MailAddress to Email
        /// </summary>
        public static implicit operator Email(MailAddress mailAddress)
        {
            return mailAddress == null ? null : new Email(mailAddress);
        }

        /// <summary>
        /// Implicit conversion from Email to MailAddress
        /// </summary>
        public static implicit operator MailAddress(Email email)
        {
            return email?._mailAddress;
        }

        /// <summary>
        /// Returns the email address as a string
        /// </summary>
        public override string ToString()
        {
            return _mailAddress?.Address ?? string.Empty;
        }

        /// <summary>
        /// Compares two Email instances for equality
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj is Email email)
            {
                return _mailAddress?.Address?.Equals(email._mailAddress?.Address, StringComparison.OrdinalIgnoreCase) ?? false;
            }
            if (obj is string str)
            {
                return _mailAddress?.Address?.Equals(str, StringComparison.OrdinalIgnoreCase) ?? false;
            }
            return false;
        }

        /// <summary>
        /// Gets the hash code for the email address
        /// </summary>
        public override int GetHashCode()
        {
            return _mailAddress?.Address?.ToLowerInvariant()?.GetHashCode() ?? 0;
        }
    }
}
