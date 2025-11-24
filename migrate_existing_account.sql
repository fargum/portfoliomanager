-- Option A: Delete the existing account and let the system auto-create
-- WARNING: This will delete all portfolio data associated with account ID 1
-- Only do this if you're comfortable re-creating your portfolio data

-- DELETE FROM app.accounts WHERE id = 1;

-- Option B: Change the account ID to preserve data but allow auto-creation
-- This moves your existing data to a different account ID, leaving ID 1 free for auto-creation

-- Step 1: Temporarily disable foreign key constraints (if needed)
-- Step 2: Update all references to account ID 1 to use account ID 999 (or another unused ID)
UPDATE app.portfolios SET account_id = 999 WHERE account_id = 1;
UPDATE app.conversation_threads SET account_id = 999 WHERE account_id = 1;
-- Add other tables that reference account_id if they exist

-- Step 3: Update the account ID itself
UPDATE app.accounts SET id = 999 WHERE id = 1;

-- Step 4: Reset the sequence to ensure ID 1 is available for auto-creation
-- (PostgreSQL automatically handles this, but you can verify with):
-- SELECT setval('app.accounts_id_seq', 1, false);

-- Now when you log in with Azure AD, the system will auto-create an account with ID 1
-- and your old data will remain under account ID 999

-- To verify the changes:
SELECT id, external_user_id, email, display_name, is_active FROM app.accounts ORDER BY id;