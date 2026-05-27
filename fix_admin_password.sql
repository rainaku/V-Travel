-- Fix admin account: replace placeholder hash with real SHA-256 hash of 'admin'
-- Run this in Supabase SQL Editor after removing the backdoor in AuthService.cs

UPDATE users
SET password_hash = '8c6976e5b5410415bde908bd4dee15dfb167a9c873fc4bb8a81f6f2ab448a918'
WHERE username = 'admin'
  AND password_hash = 'admin_hash_placeholder';
