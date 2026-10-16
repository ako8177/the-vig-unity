// Bundle export.ts with the TS game's esbuild (../the-vig/node_modules) and run it.
import { execFileSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { resolve } from 'node:path';

const vig = resolve('../the-vig');
const require = createRequire(vig + '/package.json');
const esbuild = require('esbuild');
const outfile = resolve('tools/golden/.export.mjs');
esbuild.buildSync({ entryPoints: [resolve('tools/golden/export.ts')], bundle: true, platform: 'node', format: 'esm', outfile, logLevel: 'warning' });
execFileSync(process.execPath, [outfile], { stdio: 'inherit' });
