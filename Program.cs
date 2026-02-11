// Program.cs
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

var pduState = new Dictionary<string, object>();
var wsClients = new List<WebSocket>();
var wsLock = new object();

// ── 가상 데이터 ──
int batt1V = 0, batt2V = 0, totalV = 0;
short batt1A = 0, batt2A = 0;
int batS = 0, batPPC = 0, batPNC = 0, batPC = 0, batNC = 0, batPP = 0;
int dcP = 0, dcN = 0, obcR = 0, ldcR = 0, invR = 0;
double dcOutV = 0, dcOutA = 0, obcV = 0, obcA = 0;
int dcTemp = -40, obcTemp = -40, dcWork = 0;
int acVr = 0, acVs = 0, acVt = 0;
string currentMode = "대기";

// ── HTTP + WebSocket 서버 ──
var http = new HttpListener();
http.Prefixes.Add("http://localhost:5000/");
http.Start();
Console.WriteLine("웹서버 시작: http://localhost:5000/");
Console.WriteLine();
Console.WriteLine("=== 가상 테스트 모드 ===");
Console.WriteLine("  1: 직렬 연결 (700V)");
Console.WriteLine("  2: 병렬 연결 (350V)");
Console.WriteLine("  3: OBC 충전");
Console.WriteLine("  4: DC 급속충전");
Console.WriteLine("  5: LDC 구동");
Console.WriteLine("  6: 인버터 구동");
Console.WriteLine("  0: 전부 OFF (대기)");
Console.WriteLine("========================");

System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
{
    FileName = "http://localhost:5000",
    UseShellExecute = true
});

// HTTP 처리
_ = Task.Run(async () =>
{
    while (true)
    {
        var ctx = await http.GetContextAsync();

        if (ctx.Request.IsWebSocketRequest)
        {
            var wsCtx = await ctx.AcceptWebSocketAsync(null);
            lock (wsLock) wsClients.Add(wsCtx.WebSocket);

            _ = Task.Run(async () =>
            {
                var buf = new byte[1024];
                try
                {
                    while (wsCtx.WebSocket.State == WebSocketState.Open)
                        await wsCtx.WebSocket.ReceiveAsync(buf, CancellationToken.None);
                }
                catch { }
                finally { lock (wsLock) wsClients.Remove(wsCtx.WebSocket); }
            });
        }
        else
        {
            string path = ctx.Request.Url!.AbsolutePath;
            if (path == "/") path = "/index.html";
            string filePath = Path.Combine(AppContext.BaseDirectory, "wwwroot" + path.Replace('/', Path.DirectorySeparatorChar));

            if (File.Exists(filePath))
            {
                byte[] fileBytes = await File.ReadAllBytesAsync(filePath);
                string ext = Path.GetExtension(filePath).ToLower();
                ctx.Response.ContentType = ext switch
                {
                    ".html" => "text/html; charset=utf-8",
                    ".css" => "text/css; charset=utf-8",
                    ".js" => "application/javascript; charset=utf-8",
                    _ => "application/octet-stream"
                };
                ctx.Response.ContentLength64 = fileBytes.Length;
                await ctx.Response.OutputStream.WriteAsync(fileBytes);
            }
            else { ctx.Response.StatusCode = 404; }
            ctx.Response.Close();
        }
    }
});

// ── 키보드 입력 스레드 ──
_ = Task.Run(() =>
{
    while (true)
    {
        var key = Console.ReadKey(true);
        switch (key.KeyChar)
        {
            case '0': // 전부 OFF
                batt1V = 0; batt2V = 0; totalV = 0;
                batt1A = 0; batt2A = 0;
                batS = 0; batPPC = 0; batPNC = 0; batPC = 0; batNC = 0; batPP = 0;
                dcP = 0; dcN = 0; obcR = 0; ldcR = 0; invR = 0;
                dcOutV = 0; dcOutA = 0; obcV = 0; obcA = 0;
                dcTemp = -40; obcTemp = -40; dcWork = 0;
                acVr = 0; acVs = 0; acVt = 0;
                currentMode = "대기";
                Console.WriteLine("  → [0] 전부 OFF");
                break;

            case '1': // 직렬 700V
                batt1V = 350; batt2V = 350; totalV = 700;
                batt1A = 10; batt2A = 10;
                batS = 1; batPC = 1; batNC = 1;
                batPPC = 0; batPNC = 0; batPP = 0;
                dcP = 0; dcN = 0; obcR = 0; ldcR = 0; invR = 0;
                dcOutV = 0; dcOutA = 0; obcV = 0; obcA = 0;
                dcWork = 0; acVr = 0; acVs = 0; acVt = 0;
                currentMode = "직렬 연결 (700V)";
                Console.WriteLine("  → [1] 직렬 연결 700V");
                break;

            case '2': // 병렬 350V
                batt1V = 350; batt2V = 350; totalV = 350;
                batt1A = 20; batt2A = 20;
                batPPC = 1; batPNC = 1; batPP = 1;
                batS = 0; batPC = 0; batNC = 0;
                dcP = 0; dcN = 0; obcR = 0; ldcR = 0; invR = 0;
                dcOutV = 0; dcOutA = 0; obcV = 0; obcA = 0;
                dcWork = 0; acVr = 0; acVs = 0; acVt = 0;
                currentMode = "병렬 연결 (350V)";
                Console.WriteLine("  → [2] 병렬 연결 350V");
                break;

            case '3': // OBC 충전
                batt1V = 350; batt2V = 350; totalV = 700;
                batt1A = -15; batt2A = -15;
                batS = 1; batPC = 1; batNC = 1; obcR = 1;
                batPPC = 0; batPNC = 0; batPP = 0;
                dcP = 0; dcN = 0; ldcR = 0; invR = 0;
                obcV = 700; obcA = 15;
                obcTemp = 45;
                acVr = 220; acVs = 220; acVt = 220;
                dcOutV = 0; dcOutA = 0; dcWork = 0;
                currentMode = "OBC 충전 중";
                Console.WriteLine("  → [3] OBC 충전 모드");
                break;

            case '4': // DC 급속충전
                batt1V = 350; batt2V = 350; totalV = 700;
                batt1A = -50; batt2A = -50;
                batS = 1; batPC = 1; batNC = 1; dcP = 1; dcN = 1;
                batPPC = 0; batPNC = 0; batPP = 0;
                obcR = 0; ldcR = 0; invR = 0;
                obcV = 0; obcA = 0;
                dcOutV = 0; dcOutA = 0; dcWork = 0;
                acVr = 0; acVs = 0; acVt = 0;
                currentMode = "DC 급속충전 중";
                Console.WriteLine("  → [4] DC 급속충전");
                break;

            case '5': // LDC 구동
                batt1V = 350; batt2V = 350; totalV = 350;
                batt1A = 5; batt2A = 5;
                batPPC = 1; batPNC = 1; batPP = 1; ldcR = 1;
                batS = 0; batPC = 0; batNC = 0;
                dcP = 0; dcN = 0; obcR = 0; invR = 0;
                dcOutV = 13.8; dcOutA = 30;
                dcTemp = 55; dcWork = 1;
                obcV = 0; obcA = 0; acVr = 0; acVs = 0; acVt = 0;
                currentMode = "LDC 구동 중";
                Console.WriteLine("  → [5] LDC 구동");
                break;

            case '6': // 인버터
                batt1V = 350; batt2V = 350; totalV = 700;
                batt1A = 100; batt2A = 100;
                batS = 1; batPC = 1; batNC = 1; invR = 1;
                batPPC = 0; batPNC = 0; batPP = 0;
                dcP = 0; dcN = 0; obcR = 0; ldcR = 0;
                dcOutV = 0; dcOutA = 0; dcWork = 0;
                obcV = 0; obcA = 0; acVr = 0; acVs = 0; acVt = 0;
                currentMode = "인버터 구동 중";
                Console.WriteLine("  → [6] 인버터 구동");
                break;
        }
    }
});

// ── 메인 브로드캐스트 루프 ──
while (true)
{
    Thread.Sleep(500);

    pduState.Clear();
    pduState["timestamp"] = DateTime.Now.ToString("HH:mm:ss.fff");

    // PDU_Status1
    var s1 = new Dictionary<string, object>
    {
        ["name"] = "PDU_Status1 (배터리 전압)",
        ["raw"] = "가상 데이터",
        ["count"] = 0,
        ["Batt1_Voltage"] = batt1V,
        ["Batt2_Voltage"] = batt2V,
        ["TotalBatt_Voltage"] = totalV
    };
    pduState["0x18FFFF10"] = s1;

    // PDU_Status2
    var s2 = new Dictionary<string, object>
    {
        ["name"] = "PDU_Status2 (배터리 전류, 릴레이)",
        ["raw"] = "가상 데이터",
        ["count"] = 0,
        ["Batt1_Current"] = (int)batt1A,
        ["Batt2_Current"] = (int)batt2A,
        ["BAT_S_RELAY"] = batS,
        ["BAT_PPC_RELAY"] = batPPC,
        ["BAT_PNC_RELAY"] = batPNC,
        ["BAT_PC_RELAY"] = batPC,
        ["BAT_NC_RELAY"] = batNC,
        ["BAT_PP_RELAY"] = batPP,
        ["DC_P_RELAY"] = dcP,
        ["DC_N_RELAY"] = dcN,
        ["OBC_RELAY"] = obcR,
        ["LDC_RELAY"] = ldcR,
        ["INV_RELAY"] = invR
    };
    pduState["0x18FFFF20"] = s2;

    // DCDC_VCU
    var dc = new Dictionary<string, object>
    {
        ["name"] = "DCDC_VCU (LDC 상태)",
        ["raw"] = "가상 데이터",
        ["count"] = 0,
        ["DC_Output_Vol"] = dcOutV,
        ["DC_Output_Cur"] = dcOutA,
        ["DC_Temp"] = dcTemp,
        ["DC_WorKStart"] = dcWork,
        ["DC_Err_Temp"] = 0,
        ["DC_ERR_IOUTO"] = 0,
        ["DC_ERR_VOUTO"] = 0,
        ["DC_ERR_VOUTU"] = 0,
        ["DC_ERR_VINV"] = 0,
        ["DC_ERR_VINU"] = 0,
        ["DC_ERR_OUT_SHORT"] = 0,
        ["DC_ERR_Hardware"] = 0,
        ["DC_CAN_OVERTIME"] = 0
    };
    pduState["0x1801E5F5"] = dc;

    // OBC_BMS_STATE1
    var obc1 = new Dictionary<string, object>
    {
        ["name"] = "OBC_BMS_STATE1 (OBC 상태)",
        ["raw"] = "가상 데이터",
        ["count"] = 0,
        ["OBC_ChargerVoltage"] = obcV,
        ["OBC_ChargerCurrent"] = obcA,
        ["OBC_Temperature"] = obcTemp,
        ["OBC_SoftVersion"] = 1,
        ["OBC_HardwareVersion"] = 1,
        ["OBC_TempAnomaly"] = 0,
        ["OBC_ACVoltageAnomaly"] = 0,
        ["OBC_StartStatus1"] = 0,
        ["OBC_ComOvertime"] = 0,
        ["OBC_BatteryConnectStatus"] = 0,
        ["OBC_Slave1StartStatus"] = 0,
        ["OBC_Slave2StartStatus"] = 0,
        ["OBC_Slave3StartStatus"] = 0
    };
    pduState["0x18FF50E5"] = obc1;

    // OBC_BMS_STATE2
    var obc2 = new Dictionary<string, object>
    {
        ["name"] = "OBC_BMS_STATE2 (AC 입력)",
        ["raw"] = "가상 데이터",
        ["count"] = 0,
        ["ACVoltage_R"] = acVr,
        ["ACVoltage_S"] = acVs,
        ["ACVoltage_T"] = acVt,
        ["ACCurrent_R"] = 0,
        ["ACCurrent_S"] = 0,
        ["ACCurrent_T"] = 0,
        ["OBC_ChargePortTemp1"] = -40,
        ["OBC_ChargePortTemp2"] = -40
    };
    pduState["0x18FE50E5"] = obc2;

    // JSON 브로드캐스트
    string json = JsonSerializer.Serialize(pduState);
    byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

    List<WebSocket> deadClients = new();
    lock (wsLock)
    {
        foreach (var ws in wsClients)
        {
            try
            {
                if (ws.State == WebSocketState.Open)
                    ws.SendAsync(jsonBytes, WebSocketMessageType.Text, true, CancellationToken.None).Wait();
                else
                    deadClients.Add(ws);
            }
            catch { deadClients.Add(ws); }
        }
        foreach (var d in deadClients) wsClients.Remove(d);
    }
}
