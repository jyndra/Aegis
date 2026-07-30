import * as esbuild from 'esbuild';

const isWatch = process.argv.includes('--watch');

const context = await esbuild.context({
  entryPoints: ['src/background.ts', 'src/content.ts', 'src/block.ts'],
  bundle: true,
  outdir: 'dist',
  target: 'es2022',
  format: 'esm',
  sourcemap: true,
});

if (isWatch) {
  await context.watch();
  console.log('Watching extension source files...');
} else {
  await context.rebuild();
  await context.dispose();
  console.log('Extension built successfully.');
}
