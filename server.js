const express = require('express');
const fs = require('fs');
const path = require('path');

const app = express();
const PORT = process.env.PORT || 3000;

// Serve keys endpoint
app.get('/api/keys', (req, res) => {
  try {
    const keysPath = path.join(__dirname, 'keys.json');
    const keys = JSON.parse(fs.readFileSync(keysPath, 'utf8'));
    res.json(keys);
  } catch (error) {
    res.status(500).json({ error: 'Failed to load keys' });
  }
});

// Health check
app.get('/health', (req, res) => {
  res.json({ status: 'ok' });
});

app.listen(PORT, () => {
  console.log(`Keys server running on http://localhost:${PORT}`);
  console.log(`Fetch keys from: http://localhost:${PORT}/api/keys`);
});
