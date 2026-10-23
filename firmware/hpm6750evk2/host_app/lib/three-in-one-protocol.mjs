// FieldGate · source: 三合一协议.dbc (WangHu V1.0), supplied 2026-09-08.
// Fixed byte layouts only; the original files and registry IDs are unchanged.
const layouts = {
  0x532: ['vehicle', [['mode', 0, 2], ['emergency', 2, 1], ['moving', 3, 1], ['light', 10, 1], ['voltage', 16, 16, false, 0.1], ['communicationFaults', 32, 4]]],
  0x533: ['leftDrive', [['faults', 0, 8], ['rpm', 8, 16, true], ['voltage', 24, 8], ['current', 32, 8, true], ['temperature', 40, 8, false, 1, -40]]],
  0x534: ['rightDrive', [['faults', 0, 8], ['rpm', 8, 16, true], ['voltage', 24, 8], ['current', 32, 8, true], ['temperature', 40, 8, false, 1, -40]]],
  0x5b0: ['work', [['deviceId', 0, 8], ['plantCommunicationFault', 8, 1], ['plantAlarm', 9, 1], ['plantRunning', 11, 1], ['shredderCommunicationFault', 16, 1], ['shredderAlarm', 17, 1], ['shredderEmergency', 18, 1], ['shredderRunning', 19, 1]]],
  0x5b2: ['fan', [['gear', 0, 4], ['reverse', 4, 1], ['rpm', 8, 16, true], ['voltage', 24, 8], ['current', 32, 8]]],
  0x5b3: ['pump', [['gear', 0, 4], ['reverse', 4, 1], ['liquidLevel', 6, 2], ['rpm', 8, 16, true], ['voltage', 24, 8], ['current', 32, 8]]],
  0x5b4: ['shredder', [['faults', 0, 8], ['rpm', 8, 16, true], ['voltage', 24, 8], ['current', 32, 8, true], ['temperature', 40, 8, false, 1, -40]]],
  0x650: ['battery', [['voltage', 0, 16, false, 0.1], ['current', 16, 16, true, 0.1], ['soc', 32, 16, false, 0.1]]],
  0x501: ['remote', [['soc', 32, 8], ['version', 40, 8]]],
  0x652: ['batteryFault', [['level', 0, 8], ['code', 8, 8], ['relayFaults', 16, 10], ['chargingFaults', 32, 8], ['vehicleCode', 40, 8]]],
};

export function decodeThreeInOneFrame(id, bytes) {
  if (!(bytes instanceof Uint8Array) || bytes.length !== 8) throw new Error('CAN_LENGTH');
  if (!Number.isInteger(id) || id < 0 || id > 0x7ff) throw new Error('CAN_ID');
  if (bytes.subarray(0, 7).reduce((a, b) => a ^ b, 0) !== bytes[7]) throw new Error('CAN_CHECKSUM');
  const layout = layouts[id];
  if (!layout) throw new Error('CAN_UNSUPPORTED');
  const [kind, fields] = layout;
  /** @type {Record<string, number | null>} */
  const values = {};
  for (const [name, start, length, signed = false, factor = 1, offset = 0] of fields) {
    let value = 0;
    for (let i = 0; i < length; i++) value += ((bytes[(start + i) >> 3] >> ((start + i) & 7)) & 1) * 2 ** i;
    if (signed && value >= 2 ** (length - 1)) value -= 2 ** length;
    values[name] = value * factor + offset;
  }
  if (values.soc !== undefined && values.soc > 100) values.soc = null;
  if (values.gear !== undefined && values.gear > 8) values.gear = null;
  if (values.mode !== undefined && values.mode > 1) values.mode = null;
  return { id, kind, counter: bytes[6] & 15, values };
}
