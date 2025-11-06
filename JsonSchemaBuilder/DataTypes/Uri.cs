using System;

namespace DevelApp.JsonSchemaBuilder.DataTypes
{
    /// <summary>
    /// Uri data type that wraps System.Uri with implicit conversions
    /// </summary>
    public class Uri
    {
        private readonly System.Uri _uri;

        /// <summary>
        /// Creates a Uri from a string
        /// </summary>
        /// <param name="uriString">Uri string</param>
        public Uri(string uriString)
        {
            if (string.IsNullOrWhiteSpace(uriString))
            {
                throw new ArgumentNullException(nameof(uriString));
            }
            _uri = new System.Uri(uriString);
        }

        /// <summary>
        /// Creates a Uri from a System.Uri
        /// </summary>
        /// <param name="uri">System.Uri instance</param>
        public Uri(System.Uri uri)
        {
            _uri = uri ?? throw new ArgumentNullException(nameof(uri));
        }

        /// <summary>
        /// Gets the underlying System.Uri
        /// </summary>
        public System.Uri SystemUri => _uri;

        /// <summary>
        /// Gets the absolute URI
        /// </summary>
        public string AbsoluteUri => _uri?.AbsoluteUri;

        /// <summary>
        /// Gets the scheme (e.g., http, https, ftp)
        /// </summary>
        public string Scheme => _uri?.Scheme;

        /// <summary>
        /// Gets the host component
        /// </summary>
        public string Host => _uri?.Host;

        /// <summary>
        /// Gets the port number
        /// </summary>
        public int Port => _uri?.Port ?? -1;

        /// <summary>
        /// Gets the path component
        /// </summary>
        public string Path => _uri?.AbsolutePath;

        /// <summary>
        /// Implicit conversion from string to Uri
        /// </summary>
        public static implicit operator Uri(string uriString)
        {
            return uriString == null ? null : new Uri(uriString);
        }

        /// <summary>
        /// Implicit conversion from Uri to string
        /// </summary>
        public static implicit operator string(Uri uri)
        {
            return uri?._uri?.AbsoluteUri;
        }

        /// <summary>
        /// Implicit conversion from System.Uri to Uri
        /// </summary>
        public static implicit operator Uri(System.Uri systemUri)
        {
            return systemUri == null ? null : new Uri(systemUri);
        }

        /// <summary>
        /// Implicit conversion from Uri to System.Uri
        /// </summary>
        public static implicit operator System.Uri(Uri uri)
        {
            return uri?._uri;
        }

        /// <summary>
        /// Returns the absolute URI as a string
        /// </summary>
        public override string ToString()
        {
            return _uri?.AbsoluteUri ?? string.Empty;
        }

        /// <summary>
        /// Compares two Uri instances for equality
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj is Uri uri)
            {
                // Handle null cases
                if (_uri == null && uri._uri == null)
                    return true;
                if (_uri == null || uri._uri == null)
                    return false;
                    
                return _uri.AbsoluteUri?.Equals(uri._uri.AbsoluteUri, StringComparison.OrdinalIgnoreCase) ?? false;
            }
            if (obj is string str)
            {
                return _uri?.AbsoluteUri?.Equals(str, StringComparison.OrdinalIgnoreCase) ?? false;
            }
            return false;
        }

        /// <summary>
        /// Gets the hash code for the URI
        /// </summary>
        public override int GetHashCode()
        {
            return _uri?.AbsoluteUri?.ToLowerInvariant()?.GetHashCode() ?? 0;
        }
    }
}
