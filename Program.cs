// Program.cs
using Kvaser.CanLib;

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
        Console.SetCursorPosition(0, 0);
        Console.WriteLine("=== PDU CID 전체 CAN 모니터 ===                              ");
        Console.WriteLine();

        foreach (var kvp in latestData.OrderBy(x => x.Key))
        {
            uint canId = kvp.Key;
            byte[] d = kvp.Value;
            string hex = BitConverter.ToString(d).Replace("-", " ");
            string name = GetMessageName(canId);
            int cnt = msgCount.ContainsKey(canId) ? msgCount[canId] : 0;

            Console.WriteLine($"[0x{canId:X8}] {name}                    ");
            Console.WriteLine($"  RAW: {hex}  (수신: {cnt}회)                    ");

            switch (canId)
            {
                // ── PDU_Status1 ──
                case 0x18FFFF10:
                    {
                        int b1v = d[0] | (d[1] << 8);
                        int b2v = d[2] | (d[3] << 8);
                        int tv = d[4] | (d[5] << 8);
                        Console.WriteLine($"  → Batt1_Voltage     = {b1v} V                    ");
                        Console.WriteLine($"  → Batt2_Voltage     = {b2v} V                    ");
                        Console.WriteLine($"  → TotalBatt_Voltage = {tv} V                    ");
                        break;
                    }

                // ── PDU_Status2 ──
                case 0x18FFFF20:
                    {
                        short b1a = (short)(d[0] | (d[1] << 8));
                        short b2a = (short)(d[2] | (d[3] << 8));
                        Console.WriteLine($"  → Batt1_Current = {b1a} A    Batt2_Current = {b2a} A                    ");
                        Console.WriteLine($"  → BAT_S={Bit(d[4], 0)} PPC={Bit(d[4], 1)} PNC={Bit(d[4], 2)} PC={Bit(d[4], 3)} NC={Bit(d[4], 4)} PP={Bit(d[4], 5)} DC_P={Bit(d[4], 6)} DC_N={Bit(d[4], 7)}                    ");
                        Console.WriteLine($"  → OBC={Bit(d[5], 0)} LDC={Bit(d[5], 1)} INV={Bit(d[5], 2)}                    ");
                        break;
                    }

                // ── OBC_BMS_STATE1 ──
                case 0x18FF50E5:
                    {
                        double chgV = (d[0] | (d[1] << 8)) * 0.1;
                        double chgA = (d[2] | (d[3] << 8)) * 0.1;
                        int obcTemp = (sbyte)d[5] - 40;
                        Console.WriteLine($"  → ChargerVoltage = {chgV:F1} V    ChargerCurrent = {chgA:F1} A                    ");
                        Console.WriteLine($"  → OBC_Temp = {obcTemp}℃    SW={d[6]} HW={d[7]}                    ");
                        Console.WriteLine($"  → TempAnom={Bit(d[4], 0)} ACVoltAnom={Bit(d[4], 1)} Start1={Bit(d[4], 2)} ComOT={Bit(d[4], 3)}                    ");
                        Console.WriteLine($"  → BattConn={Bit(d[4], 4)} Slave1={Bit(d[4], 5)} Slave2={Bit(d[4], 6)} Slave3={Bit(d[4], 7)}                    ");
                        break;
                    }

                // ── OBC_BMS_STATE2 ──
                case 0x18FE50E5:
                    {
                        Console.WriteLine($"  → AC_Voltage  R={d[0] * 2}V  S={d[1] * 2}V  T={d[2] * 2}V                    ");
                        Console.WriteLine($"  → AC_Current  R={d[3]}A  S={d[4]}A  T={d[5]}A                    ");
                        Console.WriteLine($"  → PortTemp1={(sbyte)d[6] - 40}℃  PortTemp2={d[7] - 40}℃                    ");
                        break;
                    }

                // ── OBC_BMS_STATE3 ──
                case 0x18FD50E5:
                    {
                        double maxCurr = (d[0] | (d[1] << 8)) * 0.1;
                        double battV = (d[3] | (d[4] << 8)) * 0.1;
                        Console.WriteLine($"  → MaxCurr={maxCurr:F1}A  CP_Duty={d[2]}%  BattV={battV:F1}V                    ");
                        Console.WriteLine($"  → PP={Bit(d[5], 0)} CP={Bit(d[5], 1)} Lock={Bit(d[5], 2)} S2={Bit(d[5], 3)} Wake={Bit(d[5], 4)} PP_Res={(d[5] >> 5) & 0x07}                    ");
                        Console.WriteLine($"  → PFC_Pos={d[6] * 2}V  PFC_Neg={d[7] * 2}V                    ");
                        break;
                    }

                // ── OBC_BMS_STATE4 ──
                case 0x18FC50E5:
                    {
                        Console.WriteLine($"  → Temp1={(sbyte)d[0] - 40}℃  Temp2={(sbyte)d[1] - 40}℃  Temp3={(sbyte)d[2] - 40}℃                    ");
                        Console.WriteLine($"  → Vin: Uvp={Bit(d[3], 0)} Ovp={Bit(d[3], 1)} LineLoss={Bit(d[3], 2)} FreqFail={Bit(d[3], 3)} PhaseLoss={Bit(d[3], 4)}                    ");
                        Console.WriteLine($"  → PFC_OC: R={Bit(d[3], 5)} S={Bit(d[3], 6)} T={Bit(d[3], 7)}                    ");
                        Console.WriteLine($"  → Bus: Uvp={Bit(d[4], 0)} Ovp={Bit(d[4], 1)} Unbal={Bit(d[4], 2)} Short={Bit(d[4], 3)}                    ");
                        Console.WriteLine($"  → Rly: Stick={Bit(d[4], 4)} Open={Bit(d[4], 5)} PfcTO={Bit(d[4], 6)} SCIFault={Bit(d[4], 7)}                    ");
                        Console.WriteLine($"  → DC: HwOcp={Bit(d[5], 0)} OvPow={Bit(d[5], 1)} OutShort={Bit(d[5], 2)} Ocp5={Bit(d[5], 3)} CaliErr={Bit(d[5], 4)}                    ");
                        Console.WriteLine($"  → OTP: Socket={Bit(d[5], 5)} PFC={Bit(d[5], 6)} M1={Bit(d[5], 7)} Air={Bit(d[6], 0)}                    ");
                        Console.WriteLine($"  → SCI: Err1={Bit(d[6], 1)} Err2={Bit(d[6], 2)} VbatErr={Bit(d[6], 3)} VoutUvp={Bit(d[6], 4)} VoutOvp={Bit(d[6], 5)}                    ");
                        Console.WriteLine($"  → Master={d[7] & 0x03} Slave1={(d[7] >> 2) & 0x03} Slave2={(d[7] >> 4) & 0x03} Slave3={(d[7] >> 6) & 0x03}                    ");
                        break;
                    }

                // ── DCDC_VCU ──
                case 0x1801E5F5:
                    {
                        double outCur = (d[3] | (d[4] << 8)) * 0.1;
                        double outVol = d[5] * 0.2;
                        int dcTemp = d[7] - 40;
                        Console.WriteLine($"  → DC_Output_Vol = {outVol:F1} V    DC_Output_Cur = {outCur:F1} A                    ");
                        Console.WriteLine($"  → DC_WorkStart = {Bit(d[6], 0)}    DC_Temp = {dcTemp}℃                    ");
                        Console.WriteLine($"  → ERR: Temp={Bit(d[1], 0)} IOUTO={Bit(d[1], 1)} VOUTO={Bit(d[1], 2)} VOUTU={Bit(d[1], 3)} VINV={Bit(d[1], 4)} VINU={Bit(d[1], 5)} SHORT={Bit(d[1], 6)} HW={Bit(d[1], 7)} CAN_OT={Bit(d[2], 0)}                    ");
                        break;
                    }

                // ── DCDC_VCU1 ──
                case 0x1801E6F5:
                    {
                        int swVer = d[0] | (d[1] << 8);
                        int hwVer = d[2] | (d[3] << 8);
                        Console.WriteLine($"  → DCDC_SW_Version = {swVer}    DCDC_HW_Version = {hwVer}                    ");
                        break;
                    }

                // ── 미정의 ──
                default:
                    {
                        Console.WriteLine($"  → (파싱 미정의)                    ");
                        break;
                    }
            }
            Console.WriteLine();
        }

        Console.WriteLine("종료: Ctrl+C                    ");
    }

    Thread.Sleep(1);
}

static string GetMessageName(uint id) => id switch
{
    0x18FFFF10 => "PDU_Status1 (배터리 전압)",
    0x18FFFF20 => "PDU_Status2 (배터리 전류, 릴레이 접점상태)",
    0x18FF50E5 => "OBC_BMS_STATE1 (OBC 상태)",
    0x18FE50E5 => "OBC_BMS_STATE2 (AC 입력 확인용)",
    0x18FD50E5 => "OBC_BMS_STATE3 (OBC 상태)",
    0x18FC50E5 => "OBC_BMS_STATE4 (OBC 상태)",
    0x1801E5F5 => "DCDC_VCU (LDC 상태)",
    0x1801E6F5 => "DCDC_VCU1 (LDC 버전)",
    _ => $"(알 수 없음 0x{id:X8})"
};

static int Bit(byte b, int pos) => (b >> pos) & 1;
