import fs from 'fs';
import path from 'path';

const distDir = './dist/frontend/server';

function fixImportsInFile(filePath) {
  let content = fs.readFileSync(filePath, 'utf8');
  const original = content;
  let modified = false;

  // 1. Corrige ".js.js" → ".js"
  content = content.replace(
    /from\s+['"](\.{1,2}\/[^'"]+?)\.js\.js(['"])/g,
    (_match, p1, q) => {
      
      modified = true;
      return `from '${p1}.js'`;
    }
  );

  // 2. Agrega ".js" si falta y NO termina ya en .js o .json
  content = content.replace(
    /from\s+['"](\.{1,2}\/[^'"]+?)(?<!\.js|\.json)(['"])/g,
    (_match, p1, q) => {
      // Solo agregar si no termina en .js ni .json
      
      modified = true;
      return `from '${p1}.js'`;
    }
  );

  if (modified) {
    fs.writeFileSync(filePath, content, 'utf8');
    
  }
}

function walkDirectory(dir) {
  const entries = fs.readdirSync(dir, { withFileTypes: true });

  for (const entry of entries) {
    const entryPath = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      walkDirectory(entryPath);
    } else if (entry.isFile() && entry.name.endsWith('.js')) {
      fixImportsInFile(entryPath);
    }
  }
}


walkDirectory(distDir);

