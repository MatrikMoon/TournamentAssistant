using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TournamentAssistant.UI.ViewControllers
{
    class ItemOpacityChanger
    {
        public static void OpacityChanger(GameObject item, float opacity) //stole this from BS+
        {
            var Image = item?.GetComponent<HMUI.ImageView>() ?? null;
            var Color = Image.color;
            Color.a = opacity;
            Image.color = Color;
        }
    }
}
