#!/usr/bin/env node

/**
 * Script to inject build timestamp into index.html
 * This allows the version checking service to detect when a new version is deployed
 */

const fs = require('fs');
const path = require('path');

const indexPath = path.join(__dirname, '../dist/browser/index.html');
const placeholder = 'BUILD_TIMESTAMP_PLACEHOLDER';
const timestamp = new Date().toISOString();

try {
  // Check if index.html exists
  if (!fs.existsSync(indexPath)) {
    console.warn(`Warning: ${indexPath} not found. Skipping version injection.`);
    process.exit(0);
  }

  // Read index.html
  let content = fs.readFileSync(indexPath, 'utf8');

  // Replace placeholder with timestamp
  if (content.includes(placeholder)) {
    content = content.replace(placeholder, timestamp);
    fs.writeFileSync(indexPath, content, 'utf8');
    console.log(`✓ Version timestamp injected: ${timestamp}`);
  } else {
    console.warn(`Warning: Placeholder "${placeholder}" not found in index.html`);
  }
} catch (error) {
  console.error('Error injecting version:', error);
  process.exit(1);
}

