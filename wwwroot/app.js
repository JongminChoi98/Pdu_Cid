let ws;

function connect() {
    ws = new WebSocket(`ws://${location.host}/`);
    ws.onopen = () => {
        document.getElementById('conn-status').textContent = '연결됨';
        document.getElementById('conn-status').className = 'connected';
    };
    ws.onclose = () => {
        document.getElementById('conn-status').textContent = '연결 끊김';
        document.getElementById('conn-status').className = 'disconnected';
        setTimeout(connect, 2000);
    };
    ws.onerror = () => ws.close();
    ws.onmessage = (e) => updateUI(JSON.parse(e.data));
}

function updateUI(data) {
    if (data.timestamp) {
        document.getElementById('timestamp').textContent = data.timestamp;
    }

    const s1 = data['0x18FFFF10'];
    const s2 = data['0x18FFFF20'];
    const dcdc = data['0x1801E5F5'];
    const obc1 = data['0x18FF50E5'];
    const obc2 = data['0x18FE50E5'];

    // ── Voltage ──
    let b1v = 0, b2v = 0, tv = 0;
    if (s1) {
        b1v = s1.Batt1_Voltage;
        b2v = s1.Batt2_Voltage;
        tv = s1.TotalBatt_Voltage;
        setText('val-b1v', b1v + ' V');
        setText('val-b2v', b2v + ' V');
        setText('val-tv', tv + ' V');
        setText('txt-b1v', b1v + ' V');
        setText('txt-b2v', b2v + ' V');
        setText('txt-total', tv + ' V');
    }

    // ── Current ──
    let b1a = 0, b2a = 0;
    if (s2) {
        b1a = s2.Batt1_Current;
        b2a = s2.Batt2_Current;
        setText('val-b1a', b1a + ' A');
        setText('val-b2a', b2a + ' A');
        setText('txt-b1a', b1a + ' A');
        setText('txt-b2a', b2a + ' A');
    }

    // ── Relays ──
    const relays = {};
    if (s2) {
        const rlyNames = [
            'BAT_S_RELAY', 'BAT_PPC_RELAY', 'BAT_PNC_RELAY', 'BAT_PC_RELAY',
            'BAT_NC_RELAY', 'BAT_PP_RELAY', 'DC_P_RELAY', 'DC_N_RELAY',
            'OBC_RELAY', 'LDC_RELAY', 'INV_RELAY'
        ];
        rlyNames.forEach(r => {
            const val = s2[r] === 1;
            relays[r] = val;
            const shortName = r.replace('_RELAY', '');

            // Data panel LED
            const led = document.getElementById('led-' + shortName);
            if (led) led.className = 'rly-led ' + (val ? 'on' : 'off');

            // SVG relay box
            const rlyG = document.getElementById('rly-' + shortName);
            if (rlyG) {
                rlyG.classList.remove('on', 'off');
                rlyG.classList.add(val ? 'on' : 'off');
            }
        });
    }

    // ── Relay shorthand ──
    const batS = relays['BAT_S_RELAY'] || false;
    const batPPC = relays['BAT_PPC_RELAY'] || false;
    const obcRly = relays['OBC_RELAY'] || false;
    const invRly = relays['INV_RELAY'] || false;
    const ldcRly = relays['LDC_RELAY'] || false;
    const dcP = relays['DC_P_RELAY'] || false;
    const dcN = relays['DC_N_RELAY'] || false;

    const anyActive = batS || batPPC;
    const hasVoltage = b1v > 0 || b2v > 0;

    // ── Wire paths ──
    setPath('path-series', batS);
    setPath('path-parallel', batPPC);
    setPath('path-dc-pos', dcP && anyActive);
    setPath('path-obc', obcRly && anyActive);
    setPath('path-obc-ac', obcRly && anyActive);
    setPath('path-inv', invRly && anyActive);
    setPath('path-inv-load', invRly && anyActive);
    setPath('path-ldc', ldcRly && anyActive);
    setPath('path-b1n', anyActive);
    setPath('path-b2n', batPPC);
    setPath('path-dc-neg', dcN);

    // ── Bus bars ──
    const busPos = document.getElementById('bus-pos');
    const busNeg = document.getElementById('bus-neg');
    if (busPos) {
        busPos.classList.toggle('active', anyActive && hasVoltage);
    }
    if (busNeg) {
        busNeg.classList.toggle('active', anyActive && hasVoltage);
    }

    // ── Component box highlights ──
    setBoxActive('box-dc', dcP);
    setBoxActive('box-obc', obcRly);
    setBoxActive('box-ac', obcRly);
    setBoxActive('box-inv', invRly);
    setBoxActive('box-load', invRly);
    setBoxActive('box-ldc', ldcRly);

    // ── Mode detection ──
    let modeText = '대기';
    let modeCls = '';

    if (dcP && anyActive) {
        modeText = 'DC 급속충전';
        modeCls = 'dc-charging';
    } else if (obcRly && anyActive) {
        modeText = 'OBC 충전';
        modeCls = 'charging';
    } else if (invRly && anyActive) {
        modeText = '인버터 구동';
        modeCls = 'inverter';
    } else if (ldcRly && anyActive) {
        modeText = 'LDC 구동';
        modeCls = 'ldc';
    } else if (batS && !batPPC) {
        modeText = '직렬 (700V)';
        modeCls = 'series';
    } else if (batPPC && !batS) {
        modeText = '병렬 (350V)';
        modeCls = 'parallel';
    }

    // Data panel mode
    const modeEl = document.getElementById('mode-display');
    if (modeEl) {
        modeEl.textContent = modeText;
        modeEl.className = 'mode-box' + (modeCls ? ' ' + modeCls : '');
    }

    // SVG mode badge
    const svgModeBg = document.getElementById('svg-mode-bg');
    const svgModeText = document.getElementById('svg-mode');
    if (svgModeBg) {
        svgModeBg.className.baseVal = 'mode-badge-bg' + (modeCls ? ' ' + modeCls : '');
    }
    if (svgModeText) {
        svgModeText.textContent = modeText;
        svgModeText.className.baseVal = 'mode-badge-text' + (modeCls ? ' ' + modeCls : '');
    }

    // ── LDC ──
    if (dcdc) {
        setText('val-ldc-v', dcdc.DC_Output_Vol + ' V');
        setText('val-ldc-a', dcdc.DC_Output_Cur + ' A');
        setText('val-ldc-t', dcdc.DC_Temp + ' \u2103');
        setText('val-ldc-work', dcdc.DC_WorKStart === 1 ? 'ON' : 'OFF');
        setText('txt-ldc', dcdc.DC_Output_Vol + 'V / ' + dcdc.DC_Output_Cur + 'A');
    }

    // ── OBC ──
    if (obc1) {
        setText('val-obc-v', obc1.OBC_ChargerVoltage + ' V');
        setText('val-obc-a', obc1.OBC_ChargerCurrent + ' A');
        setText('val-obc-t', obc1.OBC_Temperature + ' \u2103');
        setText('txt-obc', obc1.OBC_ChargerVoltage + 'V / ' + obc1.OBC_ChargerCurrent + 'A');
    }

    // ── AC ──
    if (obc2) {
        setText('val-ac', obc2.ACVoltage_R + ' / ' + obc2.ACVoltage_S + ' / ' + obc2.ACVoltage_T + ' V');
        setText('txt-ac', obc2.ACVoltage_R + ' V');
    }
}

// ── Helpers ──

function setText(id, val) {
    const el = document.getElementById(id);
    if (el) el.textContent = val;
}

function setPath(id, on) {
    const g = document.getElementById(id);
    if (!g) return;
    g.classList.remove('on', 'off');
    g.classList.add(on ? 'on' : 'off');
}

function setBoxActive(id, on) {
    const el = document.getElementById(id);
    if (!el) return;
    el.classList.toggle('active', on);
}

connect();
