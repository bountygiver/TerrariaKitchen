using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TerrariaKitchen
{
    public static class MenuGenerator
    {
        private static readonly string MenuDataStart = @"
<!DOCTYPE html>
<html lang=""en"">
    <head>
        <title>Terraria Kitchen Menu</title>
        <meta charset=""UTF-8"">
        <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
        <link rel=""stylesheet"" href=""https://www.w3schools.com/w3css/4/w3.css"">
        <style>
            body {font-family: ""Times New Roman"", Georgia, Serif;}
            h1, h2, h3, h4, h5, h6 {
              font-family: ""Playfair Display"";
              letter-spacing: 5px;
            }
            #menu {
                display: flex;
                flex-direction: row;
                flex-wrap: wrap;
                justify-content: space-between;
            }
            #menu > * {
                width: 100%;
                margin-bottom: 1rem;
            }
            #menu > div {
                width: 50%;
                margin-bottom: 2rem;
            }
        </style>
    </head>
    <body>
        <div class=""w3-content"" style=""max-width:1100px"">
            <div class=""w3-row w3-padding-64"">
                <div class=""w3-col w3-padding-large"" id=""menu"">
                    <h1 class=""w3-center"">Our Menu</h1><br>
";

        private static readonly string MenuDataEnd = @"
                </div>        
            </div>
        </div>
    </body>
</html>
";

        private static string MenuGen(KitchenConfig.KitchenEntry entry)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<div>");
            sb.AppendLine($"<h4>{entry.MobName}</h4>");
            if (entry.MobAlias != null)
            {
                sb.AppendLine($"<div class=\"w3-text-grey\">Aliases: {string.Join(", ", entry.MobAlias)}</div>");

            }
            if (!entry.Pooling)
            {
                sb.AppendLine($"<div class=\"w3-text-grey\">{entry.Price} credits per order</div>");
                sb.AppendLine($"<div class=\"w3-text-grey\">Max of {entry.MaxBuys} per order</div>");
            }
            else
            {
                sb.AppendLine($"<div class=\"w3-text-grey\">{entry.Price} credits in total needed to spawn. May be paid partially by different customers.</div>");

            }
            sb.AppendLine("</div>");

            return sb.ToString();
        }
        private static string MenuGen2(KitchenEvent entry)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<div>");
            sb.AppendLine($"<h4>{entry.EventName}</h4>");
            if (entry.EventAlias != null)
            {
                sb.AppendLine($"<div class=\"w3-text-grey\">Aliases: {string.Join(", ", entry.EventAlias)}</div>");

            }
            sb.AppendLine($"<div class=\"w3-text-grey\">{entry.Price} credits in total needed to start. May be paid partially by different customers.</div>");
            sb.AppendLine("</div>");

            return sb.ToString();
        }

        public static string GenerateMenu(List<KitchenConfig.KitchenEntry> menuEntries, List<KitchenEvent> events)
        {
            return MenuDataStart + "<h2 class=\"\"w3-center\"\">Mob Spawns</h1>" + string.Join("", menuEntries.Select(MenuGen)) + "<h2 class=\"\"w3-center\"\">Events</h1>" + string.Join("", events.Select(MenuGen2)) + MenuDataEnd;
        }
    }
}
