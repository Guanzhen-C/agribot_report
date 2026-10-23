// Offline test fixtures only. Never pass these synthetic commissioning records to hardware.
import { readFile,writeFile } from 'node:fs/promises';
import { join } from 'node:path';
import { compileMachineProfile } from '../lib/machine-profile.mjs';

const directory=process.argv[2];
if(!directory) throw new Error('Temporary test directory required');
for(const [file,key] of [['three-in-one-original','native'],['ackermann-four-lights.example','ackermann'],['differential-work.example','differential']]) {
  const profile=JSON.parse(await readFile(new URL(`../../profiles/${file}.json`,import.meta.url),'utf8'));
  await writeFile(join(directory,`${key}-draft.fgmp`),compileMachineProfile(profile),{flag:'wx'});
  profile.identity.commissioned=true;
  profile.identity.description='OFFLINE SYNTHETIC TEST FIXTURE, NOT FOR HARDWARE';
  await writeFile(join(directory,`${key}-test.fgmp`),compileMachineProfile(profile),{flag:'wx'});
}
