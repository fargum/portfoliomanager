# Security Guidelines

## 🚨 CRITICAL - Before Committing to GitHub

### Database Credentials & API Keys

1. **Never commit actual credentials to version control**
2. **Check these files before pushing:**
   - `.env` - Should contain placeholder values only
   - `PortfolioManagerDbContext.cs` - Should not contain hardcoded passwords
   - Any configuration files with connection strings

### Environment Variables

The following environment variables contain sensitive information:

- `DB_PASSWORD` - Database password
- `EOD_API_TOKEN` - EOD Historical Data API token

### Secure Setup Process

1. **Copy `.env.example` to `.env`:**
   ```bash
   cp .env.example .env
   ```

2. **Update `.env` with your actual credentials:**
   ```bash
   DB_PASSWORD=your_actual_secure_password
   EOD_API_TOKEN=your_actual_api_token
   ```

3. **Verify `.env` is in `.gitignore`:**
   ```bash
   grep -n "\.env" .gitignore
   ```

### Production Deployment

For production environments:

1. **Use environment variables or secrets management**
2. **Consider Azure Key Vault, AWS Secrets Manager, or similar**
3. **Never store secrets in configuration files**
4. **Use strong, unique passwords**

### Before Each Commit

Run this checklist:

- [ ] No real passwords in any committed files
- [ ] No API tokens in any committed files  
- [ ] `.env` file contains only placeholder values
- [ ] All sensitive data uses environment variables

### Emergency Response

If credentials are accidentally committed:

1. **Immediately rotate all exposed credentials**
2. **Remove from git history using `git filter-branch` or BFG Repo-Cleaner**
3. **Force push to remote repository**
4. **Audit access logs for any unauthorized usage**

## Contact

For security concerns, please create a private issue or contact the repository maintainers directly.