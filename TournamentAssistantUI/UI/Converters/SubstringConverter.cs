using System;
using System.Globalization;
using System.Windows.Data;

/*
 * File created by Moon on 1/20/2022
 * https://blog.wepredict.co.uk/2013/04/16/implementing-a-substring-converter-for-use-in-xaml-bindings/
 */

namespace TournamentAssistantUI.UI.Converters
{
    public class SubstringConverter : IValueConverter
    {
        #region IValueConverter Members
        /*
         * Return a substring of the input string using the same notation as SubString()
         *
         * Parameter startIndex | startIndex,length
         *
         * To obtain the functionality of length = (string.length - x) for the length, provide a negative length value
         */

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            //Input as String
            var val = (string)value;

            //Attempt to split parameters by comma
            var parameters = ((string)parameter).Split(',');

            //invalid or no parameters, return full string
            if (parameters == null || parameters.Length == 0)
                return val;

            //return remaining string after startIndex
            if (parameters.Length == 1)
            {
                return val.Substring(int.Parse(parameters[0]));
            }

            //return length characters of string after startIndex
            if (parameters.Length >= 2)
            {
                var startIndex = int.Parse(parameters[0]);
                var length = int.Parse(parameters[1]);

                if (length >= 0)
                {
                    return val.Substring(startIndex, length);
                }

                //negative length was provided
                return val.Substring(startIndex, val.Length + length);
            }

            return val;
        }

        /*
         * Not Implemented.
         */
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }

        #endregion
    }
}
