import { PageHeading } from '../console-shell';
import { VcuConsole } from '../vcu-console';

export const metadata = { title: '设备设置 · FieldGate' };
export default function Page() {
  return <><PageHeading title="设备设置"/><VcuConsole/></>;
}
