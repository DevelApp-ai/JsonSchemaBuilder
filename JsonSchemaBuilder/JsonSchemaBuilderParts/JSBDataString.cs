using DevelApp.Utility.Model;
using System;

namespace DevelApp.JsonSchemaBuilder.JsonSchemaParts
{
    /// <summary>
    /// Data string class to potentially replace standard string with enhanced functionality
    /// TODO: Implement full DataString class with System.Net.Mail.MailAddress integration or other enhanced string capabilities
    /// </summary>
    public class JSBDataString : JSBString
    {
        public JSBDataString(
                IdentifierString dataStringName,
                string description,
                string defaultValue = null,
                bool isRequired = false)
            : base(dataStringName,
                description,
                defaultValue: defaultValue,
                isRequired: isRequired)
        {
            // TODO: Add enhanced data string functionality
        }

        public override JSBPartType PartType
        {
            get
            {
                return JSBPartType.DataString;
            }
        }
    }
}
