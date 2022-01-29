using Google.Protobuf.Collections;
using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using TournamentAssistantShared;

/*
 * File created by Moon on 1/28/2022
 * https://stackoverflow.com/questions/20707160/data-binding-int-property-to-enum-in-wpf
 */

namespace TournamentAssistantUI.UI.Converters
{
    public class DifficultyEnumConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is RepeatedField<int>)
            {
                return (value as RepeatedField<int>).Select(x => (SharedConstructs.BeatmapDifficulty)x).ToList();
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (int)Enum.Parse(typeof(SharedConstructs.BeatmapDifficulty), value.ToString());
        }
    }
}
