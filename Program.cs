// Program.cs
using Kvaser.CanLib;

Console.WriteLine("=== 전체 CAN 메시지 모니터 ===");
Console.WriteLine("종료: Ctrl+C");

Canlib.canInitializeLibrary();
int handle = Canlib.canOpenChannel(0, Canlib.canOPEN_ACCEPT_VIRTUAL);
if (handle < 0)
{
    Console.WriteLine($"CAN 채널 열기 실패: {handle}");
    Console.ReadKey();
    return;
}
Canlib.canSetBusParams(handle, Canlib.canBITRATE_500K, 0, 0, 0, 0);
Canlib.canBusOn(handle);

var latestData = new Dictionary<uint, byte[]>();
var msgCount = new Dictionary<uint, int>();

DateTime lastPrint = DateTime.MinValue;

while (true)
{
    byte[] data = new byte[8];
    int id = 0, dlc = 0, flags = 0;
    long timestamp = 0;

    var status = Canlib.canRead(handle, out id, data, out dlc, out flags, out timestamp);

    if (status == Canlib.canStatus.canOK)
    {
        uint uid = (uint)id;
        latestData[uid] = (byte[])data.Clone();
        if (!msgCount.ContainsKey(uid)) msgCount[uid] = 0;
        msgCount[uid]++;
    }

    if ((DateTime.Now - lastPrint).TotalMilliseconds >= 1000)
    {
        lastPrint = DateTime.Now;
        Console.Clear();
        // 초기 메시지 없음 — 루프 안에서 출력

        foreach (var kvp in latestData.OrderBy(x => x.Key))
        {
            uint canId = kvp.Key;
            byte[] d = kvp.Value;
            string hex = BitConverter.ToString(d).Replace("-", " ");
            string name = GetMessageName(canId);
            int cnt = msgCount.ContainsKey(canId) ? msgCount[canId] : 0;

            Console.WriteLine($"[0x{canId:X8}] {name}");
            Console.WriteLine($"  RAW: {hex}  (수신: {cnt}회)");
            Console.WriteLine($"  B[0]={d[0]:X2} B[1]={d[1]:X2} B[2]={d[2]:X2} B[3]={d[3]:X2} B[4]={d[4]:X2} B[5]={d[5]:X2} B[6]={d[6]:X2} B[7]={d[7]:X2}");

            PrintParsed(canId, d);
            Console.WriteLine();
        }

        Console.WriteLine("종료: Ctrl+C");
    }

    Thread.Sleep(1);
}

// ── 메시지 이름 매핑 ──
static string GetMessageName(uint id) => id switch
{
    0x18FFFF10 => "PDU_Status1 (배터리 전압)",
    0x18FFFF20 => "PDU_Status2 (배터리 전류, 릴레이 접점상태)",
    0x18FF50E5 => "OBC_BMS_STATE1 (OBC 상태)",
    0x18FE50E5 => "OBC_BMS_STATE2 (AC 입력 확인용)",
    0x18FD50E5 => "OBC_BMS_STATE3 (OBC 상태)",
    0x18FC50E5 => "OBC_BMS_STATE4 (OBC 상태)",
    0x1806E5F4 => "BMS_OBC_SetCommand (OBC 명령)",
    0x1810F5E5 => "VCU_DCDC (LDC 제어)",
    0x1801E5F5 => "DCDC_VCU (LDC 상태 2)",
    0x1801E6F5 => "DCDC_VCU1 (LDC 상태)",
    _ => "(알 수 없음)"
};

// ── 메시지별 파싱 출력 ──
static void PrintParsed(uint id, byte[] d)
{
    switch (id)
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // PDU_Status1 (0x18FFFF10) — 배터리 전압
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        case 0x18FFFF10:
            {
                int batt1V = d[0] | (d[1] << 8);
                int batt2V = d[2] | (d[3] << 8);
                int totalV = d[4] | (d[5] << 8);
                Console.WriteLine($"  → Batt1_Voltage    = {batt1V} V");
                Console.WriteLine($"  → Batt2_Voltage    = {batt2V} V");
                Console.WriteLine($"  → TotalBatt_Voltage= {totalV} V");
                break;
            }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // PDU_Status2 (0x18FFFF20) — 배터리 전류 + 릴레이
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        case 0x18FFFF20:
            {
                short batt1A = (short)(d[0] | (d[1] << 8));
                short batt2A = (short)(d[2] | (d[3] << 8));
                Console.WriteLine($"  → Batt1_Current    = {batt1A} A");
                Console.WriteLine($"  → Batt2_Current    = {batt2A} A");
                Console.WriteLine($"  → BAT_S_RELAY   = {Bit(d[4], 0)}  BAT_PPC_RELAY = {Bit(d[4], 1)}  BAT_PNC_RELAY = {Bit(d[4], 2)}");
                Console.WriteLine($"  → BAT_PC_RELAY  = {Bit(d[4], 3)}  BAT_NC_RELAY  = {Bit(d[4], 4)}  BAT_PP_RELAY  = {Bit(d[4], 5)}");
                Console.WriteLine($"  → DC_P_RELAY    = {Bit(d[4], 6)}  DC_N_RELAY    = {Bit(d[4], 7)}");
                Console.WriteLine($"  → OBC_RELAY     = {Bit(d[5], 0)}  LDC_RELAY     = {Bit(d[5], 1)}  INV_RELAY     = {Bit(d[5], 2)}");
                break;
            }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // BMS_OBC_SetCommand (0x1806E5F4) — OBC 명령
        // DBC에 별도 Signal 정의 없음 → RAW 출력
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        case 0x1806E5F4:
            {
                Console.WriteLine($"  → BMS→OBC 명령 RAW: {d[0]:X2} {d[1]:X2} {d[2]:X2} {d[3]:X2} {d[4]:X2} {d[5]:X2} {d[6]:X2} {d[7]:X2}");
                break;
            }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // VCU_DCDC (0x1810F5E5) — LDC 제어
        // DBC에 별도 Signal 정의 없음 → RAW 출력
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        case 0x1810F5E5:
            {
                Console.WriteLine($"  → VCU→DCDC 제어 RAW: {d[0]:X2} {d[1]:X2} {d[2]:X2} {d[3]:X2} {d[4]:X2} {d[5]:X2} {d[6]:X2} {d[7]:X2}");
                break;
            }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // DCDC_VCU1 (0x1801E6F5) — LDC SW/HW 버전
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        case 0x1801E6F5:
            {
                int swVer = d[0] | (d[1] << 8);
                int hwVer = d[2] | (d[3] << 8);
                Console.WriteLine($"  → DCDC_SW_Version  = {swVer}");
                Console.WriteLine($"  → DCDC_HW_Version  = {hwVer}");
                break;
            }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // DCDC_VCU (0x1801E5F5) — LDC 상태 2
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        case 0x1801E5F5:
            {
                int errTemp = Bit(d[1], 0);
                int errIoutO = Bit(d[1], 1);
                int errVoutO = Bit(d[1], 2);
                int errVoutU = Bit(d[1], 3);
                int errVinV = Bit(d[1], 4);
                int errVinU = Bit(d[1], 5);
                int errOutShort = Bit(d[1], 6);
                int errHardware = Bit(d[1], 7);
                int canOvertime = Bit(d[2], 0);
                double outCur = (d[3] | (d[4] << 8)) * 0.1;
                double outVol = d[5] * 0.2;
                int workStart = Bit(d[6], 0);
                int dcTemp = d[7] - 40;

                Console.WriteLine($"  → DC_Output_Vol    = {outVol:F1} V");
                Console.WriteLine($"  → DC_Output_Cur    = {outCur:F1} A");
                Console.WriteLine($"  → DC_WorKStart     = {workStart}");
                Console.WriteLine($"  → DC_Temp          = {dcTemp} ℃");
                Console.WriteLine($"  → ERR: Temp={errTemp} IOUTO={errIoutO} VOUTO={errVoutO} VOUTU={errVoutU} VINV={errVinV} VINU={errVinU} SHORT={errOutShort} HW={errHardware} CAN_OT={canOvertime}");
                break;
            }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // OBC_BMS_STATE1 (0x18FF50E5)
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        case 0x18FF50E5:
            {
                double chgV = (d[0] | (d[1] << 8)) * 0.1;
                double chgA = (d[2] | (d[3] << 8)) * 0.1;
                int tempAnomaly = Bit(d[4], 0);
                int acVoltAnomaly = Bit(d[4], 1);
                int startStatus1 = Bit(d[4], 2);
                int comOvertime = Bit(d[4], 3);
                int battConnect = Bit(d[4], 4);
                int slave1Start = Bit(d[4], 5);
                int slave2Start = Bit(d[4], 6);
                int slave3Start = Bit(d[4], 7);
                int obcTempC = (sbyte)d[5] - 40;
                int swVer = d[6];
                int hwVer = d[7];

                Console.WriteLine($"  → OBC_ChargerVoltage = {chgV:F1} V");
                Console.WriteLine($"  → OBC_ChargerCurrent = {chgA:F1} A");
                Console.WriteLine($"  → OBC_Temperature    = {obcTempC} ℃");
                Console.WriteLine($"  → SW_Ver={swVer}  HW_Ver={hwVer}");
                Console.WriteLine($"  → TempAnomaly={tempAnomaly} ACVoltAnomaly={acVoltAnomaly} StartSt1={startStatus1} ComOT={comOvertime}");
                Console.WriteLine($"  → BattConnect={battConnect} Slave1={slave1Start} Slave2={slave2Start} Slave3={slave3Start}");
                break;
            }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // OBC_BMS_STATE2 (0x18FE50E5) — AC 입력 확인용
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        case 0x18FE50E5:
            {
                int acVr = d[0] * 2;
                int acVs = d[1] * 2;
                int acVt = d[2] * 2;
                int acAr = d[3];
                int acAs = d[4];
                int acAt = d[5];
                int portTemp1 = (sbyte)d[6] - 40;
                int portTemp2 = d[7] - 40;

                Console.WriteLine($"  → AC_Voltage  R={acVr}V  S={acVs}V  T={acVt}V");
                Console.WriteLine($"  → AC_Current  R={acAr}A  S={acAs}A  T={acAt}A");
                Console.WriteLine($"  → ChargePortTemp1={portTemp1}℃  ChargePortTemp2={portTemp2}℃");
                break;
            }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // OBC_BMS_STATE3 (0x18FD50E5)
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        case 0x18FD50E5:
            {
                double maxCurr = (d[0] | (d[1] << 8)) * 0.1;
                int cpDuty = d[2];
                double battV = (d[3] | (d[4] << 8)) * 0.1;
                int ppStatus = Bit(d[5], 0);
                int cpStatus = Bit(d[5], 1);
                int lockCharge = Bit(d[5], 2);
                int s2Status = Bit(d[5], 3);
                int wakeStatus = Bit(d[5], 4);
                int ppResistor = (d[5] >> 5) & 0x07;
                int pfcPosV = d[6] * 2;
                int pfcNegV = d[7] * 2;

                Console.WriteLine($"  → ChargingPiletMaxCurr = {maxCurr:F1} A");
                Console.WriteLine($"  → CP_Duty              = {cpDuty} %");
                Console.WriteLine($"  → BatteryVoltage       = {battV:F1} V");
                Console.WriteLine($"  → PP={ppStatus} CP={cpStatus} Lock={lockCharge} S2={s2Status} Wake={wakeStatus} PP_Res={ppResistor}");
                Console.WriteLine($"  → PFC_Pos={pfcPosV}V  PFC_Neg={pfcNegV}V");
                break;
            }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // OBC_BMS_STATE4 (0x18FC50E5)
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        case 0x18FC50E5:
            {
                int temp1 = (sbyte)d[0] - 40;
                int temp2 = (sbyte)d[1] - 40;
                int temp3 = (sbyte)d[2] - 40;

                int vinUvp = Bit(d[3], 0);
                int vinOvp = Bit(d[3], 1);
                int lineNLoss = Bit(d[3], 2);
                int lineFreqFail = Bit(d[3], 3);
                int phaseLoss = Bit(d[3], 4);
                int pfcOcR = Bit(d[3], 5);
                int pfcOcS = Bit(d[3], 6);
                int pfcOcT = Bit(d[3], 7);

                int busUvp = Bit(d[4], 0);
                int busOvp = Bit(d[4], 1);
                int busUnbalance = Bit(d[4], 2);
                int busShort = Bit(d[4], 3);
                int rlyStick = Bit(d[4], 4);
                int rlyOpen = Bit(d[4], 5);
                int pfcStartTO = Bit(d[4], 6);
                int sciCommFault = Bit(d[4], 7);

                int dcHwOcp = Bit(d[5], 0);
                int ovPow = Bit(d[5], 1);
                int outShort = Bit(d[5], 2);
                int outOcp5 = Bit(d[5], 3);
                int caliErr = Bit(d[5], 4);
                int chargerSocketOtp = Bit(d[5], 5);
                int pfcOtp = Bit(d[5], 6);
                int m1Otp = Bit(d[5], 7);

                int airOtp = Bit(d[6], 0);
                int sci1Err = Bit(d[6], 1);
                int sci2Err = Bit(d[6], 2);
                int vbatErr = Bit(d[6], 3);
                int voutUvp = Bit(d[6], 4);
                int voutOvp = Bit(d[6], 5);

                int masterSt = d[7] & 0x03;
                int slaver1St = (d[7] >> 2) & 0x03;
                int slaver2St = (d[7] >> 4) & 0x03;
                int slaver3St = (d[7] >> 6) & 0x03;

                Console.WriteLine($"  → Temp1={temp1}℃  Temp2={temp2}℃  Temp3={temp3}℃");
                Console.WriteLine($"  → Vin: Uvp={vinUvp} Ovp={vinOvp} LineLoss={lineNLoss} FreqFail={lineFreqFail} PhaseLoss={phaseLoss}");
                Console.WriteLine($"  → PFC_OC: R={pfcOcR} S={pfcOcS} T={pfcOcT}");
                Console.WriteLine($"  → Bus: Uvp={busUvp} Ovp={busOvp} Unbal={busUnbalance} Short={busShort}");
                Console.WriteLine($"  → Relay: Stick={rlyStick} Open={rlyOpen} PfcStartTO={pfcStartTO} SCIFault={sciCommFault}");
                Console.WriteLine($"  → DC: HwOcp={dcHwOcp} OvPow={ovPow} OutShort={outShort} Ocp5={outOcp5} CaliErr={caliErr}");
                Console.WriteLine($"  → OTP: Socket={chargerSocketOtp} PFC={pfcOtp} M1={m1Otp} Air={airOtp}");
                Console.WriteLine($"  → SCI: Err1={sci1Err} Err2={sci2Err} VbatErr={vbatErr} VoutUvp={voutUvp} VoutOvp={voutOvp}");
                Console.WriteLine($"  → Master={masterSt} Slave1={slaver1St} Slave2={slaver2St} Slave3={slaver3St}");
                break;
            }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // BMS_OBC_SetCommand (0x181C56F4)
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        case 0x181C56F4:
            {
                Console.WriteLine($"  → BMS→OBC 명령 RAW: {d[0]:X2} {d[1]:X2} {d[2]:X2} {d[3]:X2} {d[4]:X2} {d[5]:X2} {d[6]:X2} {d[7]:X2}");
                break;
            }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 매핑되지 않은 ID
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        default:
            {
                Console.WriteLine($"  → (파싱 미정의)");
                break;
            }
    }
}

static int Bit(byte b, int pos) => (b >> pos) & 1;
