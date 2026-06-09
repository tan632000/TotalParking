const { execSync } = require('child_process');
const fs = require('fs');
const path = require('path');

const srcDir = path.resolve(__dirname, '../dist');
const destDir = path.resolve(__dirname, '../../TotalParking/SCADALayout');

function deleteFolderRecursive(directoryPath) {
  if (fs.existsSync(directoryPath)) {
    fs.readdirSync(directoryPath).forEach((file) => {
      const curPath = path.join(directoryPath, file);
      if (fs.lstatSync(curPath).isDirectory()) {
        deleteFolderRecursive(curPath);
      } else {
        fs.unlinkSync(curPath);
      }
    });
    fs.rmdirSync(directoryPath);
  }
}

function copyFolderRecursive(src, dest) {
  const exists = fs.existsSync(src);
  const stats = exists && fs.statSync(src);
  const isDirectory = exists && stats.isDirectory();
  if (isDirectory) {
    if (!fs.existsSync(dest)) {
      fs.mkdirSync(dest, { recursive: true });
    }
    fs.readdirSync(src).forEach((childItemName) => {
      copyFolderRecursive(path.join(src, childItemName), path.join(dest, childItemName));
    });
  } else {
    fs.copyFileSync(src, dest);
  }
}

try {
  console.log('Running npm run build...');
  execSync('npm run build', { cwd: path.resolve(__dirname, '..'), stdio: 'inherit' });
  console.log('Build completed successfully.');

  console.log(`Clearing old build at: ${destDir}`);
  deleteFolderRecursive(destDir);

  console.log(`Copying new build from ${srcDir} to ${destDir}...`);
  copyFolderRecursive(srcDir, destDir);
  console.log('Assets copied successfully!');
} catch (error) {
  console.error('An error occurred during build and copy process:', error.message);
  process.exit(1);
}
