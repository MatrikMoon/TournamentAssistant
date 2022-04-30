using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

/*
 * File created by Moon on 2/14/2022
 * https://stackoverflow.com/questions/20707160/data-binding-int-property-to-enum-in-wpf
 */

namespace TournamentAssistantUI.UI.Converters
{
    public class CharacteristicConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var characteristicList = value as List<TournamentAssistantShared.Models.Characteristic>;
            if (characteristicList != null)
            {
                return characteristicList.Select(x => x.SerializedName).ToList();
            }
            return "N/A";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
