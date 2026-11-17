"""Legal boundaries and invalid requests through the physical X5 client and ECU."""
import argparse
import json
from pathlib import Path
import sys
import time

import rclpy
from geometry_msgs.msg import Twist
from std_msgs.msg import String


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args()
    scripts=args.root.resolve()/'ros2_ws/src/agribot_hil_hitch/scripts'
    sys.path.insert(0,str(scripts))
    from verify_powertrain import Bench
    from powertrain_wire import MODES
    args.physical=True
    args.output.mkdir(parents=True,exist_ok=False)

    class ParameterBench(Bench):
        def __init__(self):
            self.request=json.dumps(dict(mode='off',gear=0,engine_rpm=1600,ratio=0,shift_ms=600,ratio_rate=150))
            super().__init__(args)

        def send(self):
            self.commands.write(json.dumps(dict(at=time.monotonic(),request=self.request))+'\n')
            self.velocity_pub.publish(Twist())
            self.hitch_pub.publish(String(data=json.dumps(dict(mode='hold',position=0,speed=0))))
            self.profile_pub.publish(String(data=self.request))

        def request_value(self,values):
            self.request=json.dumps(values)

        def reset(self):
            self.request_value(dict(mode='off',gear=0,engine_rpm=1600,ratio=0,shift_ms=600,ratio_rate=150))
            self.check('reset_'+str(len(self.report['cases'])),self.pump(5.,lambda:self.state is not None and
                self.state[19]==0 and self.state[15]==1 and abs(self.state[10])<.02))
            self.pump(.6)

    rclpy.init()
    node=ParameterBench()
    baseline=dict(mode='cvt_manual',gear=0,engine_rpm=1600,ratio=450,shift_ms=600,ratio_rate=150)
    try:
        node.configure_auth(False)
        node.check('physical_ready',node.pump(25.,lambda:node.ready and node.state is not None))
        node.configure_auth(True)
        legal=[dict(baseline,engine_rpm=800,ratio=200,shift_ms=200,ratio_rate=10),
               dict(baseline,engine_rpm=2200,ratio=900,shift_ms=2000,ratio_rate=300),
               dict(baseline,engine_rpm=820,ratio=450,shift_ms=210,ratio_rate=11),
               dict(baseline,mode='powershift_manual',gear=4,ratio=0),
               dict(baseline,mode='cvt_auto',ratio=0)]
        for index,value in enumerate(legal,1):
            node.reset()
            began=len(node.history)
            node.request_value(value)
            node.pump(1.)
            mode=MODES.index(value['mode'])
            acks=[bytes.fromhex(e['data']) for e in node.history[began:] if e['kind']=='powertrain_ack']
            accepted=any(a[1]==0 and a[4]==0 and a[2]==mode and a[3]==value['gear'] for a in acks)
            settings=any(a[1]==0 and a[4]==1 for a in acks)
            node.check(f'legal_boundary_{index}',accepted and settings and node.state[0]==mode,
                       dict(request=value,controller_ack=accepted,settings_ack=settings))
        invalid=[dict(baseline,engine_rpm=799),dict(baseline,engine_rpm=2201),dict(baseline,engine_rpm=810),
                 dict(baseline,engine_rpm=True),dict(baseline,shift_ms=199),dict(baseline,shift_ms=2001),
                 dict(baseline,shift_ms=205),dict(baseline,ratio_rate=9),dict(baseline,ratio_rate=301),
                 dict(baseline,ratio=199),dict(baseline,ratio=901),dict(baseline,ratio=True),
                 dict(baseline,mode='invalid_mode'),dict(baseline,mode='cvt_auto',gear=1,ratio=0),
                 dict(baseline,mode='powershift_manual',gear=0,ratio=0),
                 dict(baseline,mode='cvt_auto',ratio=450),dict(baseline,unexpected=True)]
        for index,value in enumerate(invalid,1):
            node.reset()
            node.request_value(baseline)
            node.check(f'before_invalid_{index}',node.pump(5.,lambda:node.state[0]==4 and node.state[19]==1))
            node.request_value(value)
            stopped=node.pump(4.,lambda:node.state[0]==0 and node.state[19]==0 and node.state[15]==1 and abs(node.state[10])<.02)
            node.pump(.3)
            node.check(f'invalid_request_{index}',stopped and node.state[19]==0,
                       dict(request=value,actual_disabled=node.state[19]==0,actual_brake=node.state[15]==1))
        node.reset()
        node.report['passed']=True
    except BaseException as error:
        node.report['error']=str(error)
        raise
    finally:
        try:
            node.request_value(dict(mode='off',gear=0,engine_rpm=1600,ratio=0,shift_ms=600,ratio_rate=150))
            node.pump(.5)
            node.configure_auth(False)
        finally:
            node.report['events']=node.history
            node.save()
            node.samples.close()
            node.commands.close()
            node.destroy_node()
            rclpy.try_shutdown()


if __name__=='__main__':
    main()
