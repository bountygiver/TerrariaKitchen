namespace TerrariaKitchen
{
    public static class KitchenOverlayHtml
    {
        private static readonly string contents = @"
<!DOCTYPE html>
<html lang=""en"">
    <head>
        <title>Terraria Kitchen Overlay</title>
        <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
        <meta charset=""utf-8"">
        <script src=""https://code.jquery.com/jquery-3.7.1.min.js"" integrity=""sha256-/JqT3SQfawRcv/BIHPThkBvs0OEvtFFmqPF/lYI/Cxo="" crossorigin=""anonymous""></script>
        <link href=""https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.min.css"" rel=""stylesheet"" integrity=""sha384-QWTKZyjpPEjISv5WaRU9OFeRpok6YctnYmDr5pNlyT2bRjXh0JMhjY6hW+ALEwIH"" crossorigin=""anonymous"">
        <script src=""https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/js/bootstrap.bundle.min.js"" integrity=""sha384-YvpcrYf0tY3lHB60NNkmXc5s9fDVZLESaAA55NDzOxhy9GkcIdslK1eN7N6jIeHz"" crossorigin=""anonymous""></script>
        <style>
            body {
                background-color: #00ff00;
                -webkit-text-fill-color: white;
                -webkit-text-stroke: 0.75px black;
            }

            .progress-bar {
                -webkit-text-fill-color: black;
                -webkit-text-stroke: 0px black;
            }

            button {
                -webkit-text-fill-color: black;
                -webkit-text-stroke: 0px black;
            }
        </style>
    </head>
    <body>
        <button id=""reconnectBtn"" type=""button"" class=""btn btn-primary"">Disconnected. Click to Reconnect after starting TShock with Terraria Kitchen Plugin</button>
        <div id=""waitKitchen"" style=""display: none;"">
            <h1 class=""mx-auto my-2 fw-bold"">Waiting for Kitchen to Open...</h1>
        </div>
        <div id=""overlayBody"" style=""display: none;"">
            <div class=""fs-5 fw-bold"">Current Top Pool:</div>
            <div id=""topPool"">
                No pools, use \""!t event &lt;event_name&gt; &lt;amount&gt;\"" to start a pool for a new event!
            </div>
            <div class=""fs-5 fw-bold"">Current Top Wave:</div>
            <div id=""topWave"">
                No waves, use ""!t wave start &lt;target size&gt;"" to start building a wave!
            </div>
            <div id=""poolCarousel"" class=""carousel slide"" data-bs-ride=""carousel"">
                <div class=""carousel-inner"">
                </div>
            </div>
            <div id=""recentMessage""></div>
        </div>
    </body>
    <script>
        var socket;
        var reconnectTimeout;

        var pools = [];
        var waves = [];

        const wsDest = ""ws://WSDESTREPLACEME"";

        $(document).ready(connectKitchen);

        function escapeHtml(unsafe)
        {
            return unsafe
                .replace(/&/g, ""&amp;"")
                .replace(/</g, ""&lt;"")
                .replace(/>/g, ""&gt;"")
                .replace(/""/g, ""&quot;"")
                .replace(/'/g, ""&#039;"");
        }

        function pushEvent(txt) {
            $(""#recentMessage"").text(txt);
            $(""#recentMessage"").animate({
                opacity: ""100%"",
            }, 5, ""swing"", () => {
                $(""#recentMessage"").animate({
                    opacity: ""0%"",
                }, 10000, ""swing"");
            });

        }

        function updateTopPool() {
            let top = $("".pool"").toArray().reduce(function(t, c) {
                if (t) {
                    let c_p = $(c).find("".progress"").attr(""aria-valuenow"") / $(c).find("".progress"").attr(""aria-valuemax"");
                    let t_p = $(t).find("".progress"").attr(""aria-valuenow"") / $(t).find("".progress"").attr(""aria-valuemax"");
                    if (c_p > t_p) {
                        return c;
                    } else {
                        return t;
                    }
                }
                return c;
            }, null);
            if (top) {
                $(""#topPool"").html($(top).html());
            } else {
                $(""#topPool"").text(""No pools, use \""!t event <event_name> <amount>\"" to start a pool for a new event!"");
            }
        }

        function updateTopWave() {
            let top = $("".wave"").toArray().reduce(function(t, c) {
                if (t) {
                    let t_p = $(t).find("".progress"").attr(""aria-valuemax"") - $(t).find("".progress"").attr(""aria-valuenow"");
                    if ($(c).find("".progress"").attr(""aria-valuenow"") > $(t).find("".progress"").attr(""aria-valuenow"")) {
                        return c;
                    } else {
                        return t;
                    }
                }
                return c;
            }, null);
            if (top) {
                $(""#topWave"").html($(top).html());
            } else {
                $(""#topWave"").text(""No waves, use \""!t wave start <target size>\"" to start building a wave!"");
            }
        }

        function endPool(p) {
            $("".pool[data-idx="" + p.idx + ""]"").remove();
            updateTopPool();
        }

        function updatePool(p) {
            let existingPool = $(""#poolCarousel"").find("".pool[data-idx="" + p.idx + ""]"");
            pushEvent(`${p.lastContributor} has contributed ${p.lastContribution} to ${existingPool.find("".pool-name"").text()}`);
            existingPool.find("".progress"").attr(""aria-valuenow"", p.current);
            let pct = p.current / existingPool.find("".progress"").attr(""aria-valuemax"");
            existingPool.find("".progress-bar"").css(""width"", `${pct * 100}%`);
            existingPool.find("".progress-bar"").text(`${p.current}/${existingPool.find("".progress"").attr(""aria-valuemax"")}`);

            updateTopPool();
        }

        function addPool(p) {
            let pool = $(""<div></div>"");
            let current = 0;
            if (p.current) {
                current = p.current;
            }
            pool.addClass(""carousel-item"");
            pool.addClass(""pool"");
            pool.attr(""data-idx"", p.idx);
            let pct = current / p.target;
            pool.html(`
            <div class=""pool-name"">${escapeHtml(p.name + ` (Contribute with ""!t pool ${p.idx} <amount>"")`)}</div>
            <div class=""progress"" role=""progressbar"" aria-label=""${escapeHtml(p.name)}"" aria-valuenow=""${p.current}"" aria-valuemin=""0"" aria-valuemax=""${p.target}"">
                <div class=""progress-bar overflow-visible text-dark bg-primary"" style=""width: ${pct * 100}%"">${p.current}/${p.target}</div>
            </div>
            `);
            $(""#poolCarousel"").find("".active"").removeClass(""active"");
            pool.addClass(""active"");
            $(""#poolCarousel"").find("".carousel-inner"").append(pool);

            updateTopPool();
        }

        function endWave(p) {
            $("".wave[data-chatter="" + p.chatter + ""]"").remove();
            updateTopWave();
        }

        function updateWave(p) {
            let existingWave = $(""#poolCarousel"").find("".wave[data-chatter='"" + escapeHtml(p.chatter) + ""']"");
            pushEvent(`${p.by} has added ${p.increment} ${p.mob} to ${existingWave.find("".wave-name"").text()}`);
            existingWave.find("".progress"").attr(""aria-valuenow"", p.current);
            let pct = p.current / existingWave.find("".progress"").attr(""aria-valuemax"");
            existingWave.find("".progress-bar"").css(""width"", `${pct * 100}%`);
            existingWave.find("".progress-bar"").text(`${p.current}/${existingWave.find("".progress"").attr(""aria-valuemax"")}`);

            updateTopWave();
        }

        function addWave(p) {
            let wave = $(""<div></div>"");
            let current = 0;
            if (p.current) {
                current = p.current;
            }
            wave.addClass(""carousel-item"");
            wave.addClass(""wave"");
            wave.attr(""data-chatter"", p.chatter);
            let pct = current / p.target;
            wave.html(`
            <div class=""wave-name"">${escapeHtml(p.chatter + `'s Wave (Contribute with ""!t wave buy ${escapeHtml(p.chatter)} <mob name> <amount>"")`)}</div>
            <div class=""progress"" role=""progressbar"" aria-label=""${escapeHtml(p.chatter)}"" aria-valuenow=""${p.current}"" aria-valuemin=""0"" aria-valuemax=""${p.target}"">
                <div class=""progress-bar overflow-visible text-dark bg-warning"" style=""width: ${pct * 100}%"">${p.current}/${p.target}</div>
            </div>
            `);
            $(""#poolCarousel"").find("".active"").removeClass(""active"");
            wave.addClass(""active"");
            $(""#poolCarousel"").find("".carousel-inner"").append(wave);

            updateTopWave();
        }

        function init(data) {
            $(""#waitKitchen"").hide();
            $(""#overlayBody"").show();
            if (data.pools && data.pools.length) {
                data.pools.forEach(addPool);
            }
            if (data.waves && data.waves.length) {
                data.waves.forEach(addWave);
            }
        }

        function handleKitchenMsg(data) {
            try {
                let d = JSON.parse(data);
                if (d.event) {
                    switch (d.event) {
                        case ""reset"":
                            pools = [];
                            waves = [];
                            $(""#poolCarousel"").find("".carousel-inner"").empty();
                            break;
                        case ""initialize"":
                            init(d);
                            break;
                        case ""poolStart"":
                            addPool(d);
                            break;
                        case ""poolUpdate"":
                            updatePool(d);
                            break;
                        case ""poolEnd"":
                            endPool(d);
                            break;
                        case ""waveStart"":
                            addWave(d);
                            break;
                        case ""waveUpdate"":
                            updateWave(d);
                            break;
                        case ""waveEnd"":
                            endWave(d);
                            break;
                    }
                }
            }
            catch {

            }
        }

        function connectKitchen() {
            if (reconnectTimeout != null) {
                clearTimeout(reconnectTimeout);
                reconnectTimeout = null;
            }
            $(""#reconnectBtn"").hide();
            socket = new WebSocket(wsDest);

            socket.addEventListener(""open"", (event) => {
                $(""#waitKitchen"").show();
            });
            socket.addEventListener(""message"", (event) => {
                handleKitchenMsg(event.data);
            });
            socket.addEventListener(""close"", (event) => {
                socket = null;
                $(""#reconnectBtn"").show();
                $(""#waitKitchen"").hide();
                $(""#overlayBody"").hide();
                if (!reconnectTimeout) {
                    reconnectTimeout = setTimeout(connectKitchen, 6000);
                }
            });
        }
    </script>
</html>
";

        public static string GeneratePage(string wsEndpoint) => contents.Replace("WSDESTREPLACEME", wsEndpoint);
    }
}
