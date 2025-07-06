#!/usr/bin/env node

const fs = require('fs');
const path = require('path');

// Get command line arguments
const args = process.argv.slice(2);
if (args.length < 2) {
    console.error('Usage: build-index.js <output-path> <source.json-path>');
    process.exit(1);
}

const outputPath = args[0];
const sourcePath = args[1];

// Read source.json
const sourceData = JSON.parse(fs.readFileSync(sourcePath, 'utf8'));

// Read package.json
const packageJsonPath = path.join('kittyncat_tools', 'cat.kittyn.enhanced-dynamics', 'package.json');
const packageData = JSON.parse(fs.readFileSync(packageJsonPath, 'utf8'));

// Create VPM package entry
const vpmPackage = {
    "name": packageData.name,
    "displayName": packageData.displayName,
    "version": packageData.version,
    "unity": packageData.unity,
    "unityRelease": packageData.unityRelease,
    "description": packageData.description,
    "author": packageData.author,
    "license": packageData.license,
    "licensesUrl": packageData.licensesUrl,
    "changelogUrl": packageData.changelogUrl,
    "documentationUrl": packageData.documentationUrl,
    "dependencies": packageData.dependencies || {},
    "vpmDependencies": packageData.vpmDependencies || {},
    "keywords": packageData.keywords || [],
    "url": packageData.url.replace("{VERSION}", packageData.version),
    "samples": packageData.samples || []
};

// Handle existing index.json
let existingData = { packages: {} };
if (fs.existsSync(outputPath)) {
    try {
        existingData = JSON.parse(fs.readFileSync(outputPath, 'utf8'));
    } catch (error) {
        console.warn('Warning: Could not parse existing index.json, creating new one');
    }
}

// Ensure packages object exists
if (!existingData.packages) {
    existingData.packages = {};
}

// Initialize or update package versions
if (!existingData.packages[packageData.name]) {
    existingData.packages[packageData.name] = {
        versions: {}
    };
}

// Add new version
existingData.packages[packageData.name].versions[packageData.version] = vpmPackage;

// Create final index.json structure
const indexData = {
    "name": sourceData.name,
    "author": sourceData.author,
    "url": sourceData.url,
    "id": sourceData.id,
    "packages": existingData.packages
};

// Write index.json
fs.mkdirSync(path.dirname(outputPath), { recursive: true });
fs.writeFileSync(outputPath, JSON.stringify(indexData, null, 2));

console.log(`Successfully built index.json with ${Object.keys(existingData.packages[packageData.name].versions).length} version(s) of ${packageData.name}`);
console.log(`Latest version: ${packageData.version}`);