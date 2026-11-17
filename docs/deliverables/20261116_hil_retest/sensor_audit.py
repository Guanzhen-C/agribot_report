"""Finite passive sensor audit, including same-stamp PC/X5 PNG pixel matching."""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import time

from cv_bridge import CvBridge
import rclpy
from rclpy.node import Node
from rclpy.qos import qos_profile_sensor_data
from sensor_msgs.msg import Image, CompressedImage, PointCloud2, Imu, NavSatFix
from rosgraph_msgs.msg import Clock


def compare_images(raw,decoded):
    if not raw or not decoded:
        return dict(passed=False,matched_frames=0,reason='Both source and decoded frames are required')
    first=max(min(r[0] for r in raw),min(r[0] for r in decoded))
    last=min(max(r[0] for r in raw),max(r[0] for r in decoded))
    # A Gazebo rendering burst can contain several different frames with the
    # same simulation stamp. Preserve multiplicity and match full RGB hashes;
    # a single-value stamp dictionary can wrongly cross-pair those frames.
    # Boundary cohorts can have started before either subscription attached.
    a=Counter(tuple(r) for r in raw if first<r[0]<last)
    b=Counter(tuple(r) for r in decoded if first<r[0]<last)
    common=a & b
    missing_raw=list((a-b).elements())
    missing_decoded=list((b-a).elements())
    return dict(passed=bool(common) and not missing_raw and not missing_decoded,
                matched_frames=sum(common.values()),evaluated_sim_stamp_open_interval=[first,last],
                unmatched_raw=missing_raw,unmatched_decoded=missing_decoded,
                duplicate_raw_stamps={str(k):v for k,v in Counter(r[0] for r in raw).items() if v>1},
                duplicate_decoded_stamps={str(k):v for k,v in Counter(r[0] for r in decoded).items() if v>1})


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output',type=Path,required=True)
    parser.add_argument('--seconds',type=float,default=30.)
    parser.add_argument('--no-camera',action='store_true')
    args=parser.parse_args()
    if args.output.exists(): raise FileExistsError(args.output)
    rclpy.init()
    node=Node('hil_acceptance_passive_sensor_audit')
    bridge=CvBridge()
    records={}
    images={'raw':[],'decoded':[]}
    errors=[]
    def receive(name,msg):
        stamp=msg.clock if isinstance(msg,Clock) else msg.header.stamp
        tick=stamp.sec*1000000000+stamp.nanosec
        entry=records.setdefault(name,dict(count=0,first_stamp=tick,last_stamp=tick,backwards=0,frames={}))
        entry['count']+=1
        if tick<entry['last_stamp']: entry['backwards']+=1
        entry['last_stamp']=tick
        frame=msg.header.frame_id if hasattr(msg,'header') else 'simulation_clock'
        entry['frames'][frame]=entry['frames'].get(frame,0)+1
        if isinstance(msg,CompressedImage): entry['formats']=msg.format
        if isinstance(msg,Image):
            try:
                data=bridge.imgmsg_to_cv2(msg,desired_encoding='rgb8')
                images[name].append([tick,frame,msg.width,msg.height,hashlib.sha256(data.tobytes()).hexdigest()])
            except Exception as error:
                errors.append(str(error))
    topics=[('clock','/clock',Clock),('lidar','/lidar/points',PointCloud2),
            ('imu','/imu/data',Imu),('gnss','/navsat/fix',NavSatFix)]
    if not args.no_camera:
        topics += [('raw','/camera/rgb/image_raw',Image),('compressed','/hil/camera/image/compressed',CompressedImage),
                   ('decoded','/hil/camera/decoded',Image)]
    subs=[node.create_subscription(kind,topic,lambda msg,name=name:receive(name,msg),qos_profile_sensor_data)
          for name,topic,kind in topics]
    started=time.monotonic()
    try:
        while time.monotonic()-started<args.seconds:
            rclpy.spin_once(node,timeout_sec=.05)
        required={name for name,_,_ in topics}
        passed=(required.issubset(records) and all(v['count']>=2 and v['last_stamp']>v['first_stamp']
                 and v['backwards']==0 for v in records.values()) and not errors)
        pixel=compare_images(images['raw'],images['decoded']) if not args.no_camera else {'not_applicable':True}
        if not args.no_camera:
            passed=passed and pixel['passed'] and pixel['matched_frames']>=3 and 'png' in records.get('compressed',{}).get('formats','').lower()
        report=dict(scope='Passive PC source and X5 decoder observations; not hardware sensor synchronization certification',
                    passed=bool(passed),wall_seconds=time.monotonic()-started,topics=records,pixel_comparison=pixel,
                    frame_hash_records=images,frame_hash_fields=['stamp','frame','width','height','rgb_sha256'],errors=errors)
        args.output.parent.mkdir(parents=True,exist_ok=True)
        args.output.write_text(json.dumps(report,indent=2)+'\n')
        print(json.dumps(dict(passed=bool(passed),counts={k:v['count'] for k,v in records.items()},
            pixel_comparison=pixel,errors=errors)))
        if not passed: raise SystemExit(1)
    finally:
        node.destroy_node()
        rclpy.try_shutdown()


if __name__=='__main__':
    main()
