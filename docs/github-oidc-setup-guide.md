# GitHub OIDC Setup for BadgeSmith AWS Deployment

## 🔧 Complete Setup Guide

### Step 1: Create OIDC Provider (One-time setup)

**AWS Console:**
1. **IAM** → **Identity providers** → **Add provider**
2. **Provider type**: `OpenID Connect`
3. **Provider URL**: `https://token.actions.githubusercontent.com`
4. **Audience**: `sts.amazonaws.com`
5. **Add provider**

**AWS CLI Alternative:**
```bash
aws iam create-open-id-connect-provider \
  --url https://token.actions.githubusercontent.com \
  --client-id-list sts.amazonaws.com \
  --thumbprint-list 6938fd4d98bab03faadb97b34396831e3780aea1
```

### Step 2: Create IAM Role

**Replace these placeholders:**
- `ACCOUNT-ID`: Your AWS Account ID (12 digits)
- `YOUR-GITHUB-USERNAME`: Your GitHub username or organization

#### 2.1 Trust Policy (`github-oidc-trust-policy.json`)
```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": {
        "Federated": "arn:aws:iam::ACCOUNT-ID:oidc-provider/token.actions.githubusercontent.com"
      },
      "Action": "sts:AssumeRoleWithWebIdentity",
      "Condition": {
        "StringEquals": {
          "token.actions.githubusercontent.com:aud": "sts.amazonaws.com"
        },
        "StringLike": {
          "token.actions.githubusercontent.com:sub": [
            "repo:YOUR-GITHUB-USERNAME/badge-smith:ref:refs/heads/master",
            "repo:YOUR-GITHUB-USERNAME/badge-smith:environment:production"
          ]
        }
      }
    }
  ]
}
```

#### 2.2 Create Role via AWS Console
1. **IAM** → **Roles** → **Create role**
2. **Trusted entity type**: `Web identity`
3. **Identity provider**: `token.actions.githubusercontent.com`
4. **Audience**: `sts.amazonaws.com`
5. **GitHub organization**: `YOUR-GITHUB-USERNAME`
6. **GitHub repository**: `badge-smith`
7. **GitHub branch**: `master`
8. **Role name**: `BadgeSmith-GitHubActions-Role`

#### 2.3 Attach Permissions Policy
Attach the custom policy from `aws-iam-policy.json` or for development use:
- `PowerUserAccess` (for development)
- Custom policy (for production)

### Step 3: Get Role ARN
Copy the Role ARN: `arn:aws:iam::ACCOUNT-ID:role/BadgeSmith-GitHubActions-Role`

### Step 4: Add to GitHub Repository Secrets
1. **Your repo** → **Settings** → **Secrets and variables** → **Actions**
2. **New repository secret**:
   - **Name**: `AWS_ROLE_ARN`
   - **Value**: `arn:aws:iam::ACCOUNT-ID:role/BadgeSmith-GitHubActions-Role`

### Step 5: Update GitHub Workflow
The workflow will use `role-to-assume` instead of access keys.

## 🔒 Security Benefits

✅ **No long-lived credentials** stored in GitHub
✅ **Automatic token rotation** (tokens expire in 1 hour)
✅ **Conditional access** (only from specific repo/branch)
✅ **Audit trail** via AWS CloudTrail
✅ **Revocable** (disable role to revoke all access)

## 🎯 Trust Policy Conditions Explained

- **`aud`**: Ensures tokens are for AWS STS
- **`sub`**: Restricts to specific repo, branch, and environment
  - `repo:username/badge-smith:ref:refs/heads/master` - Only master branch
  - `repo:username/badge-smith:environment:production` - Only production environment

## 🛠️ CLI Commands (Alternative)

```bash
# Create role (replace ACCOUNT-ID and trust policy file path)
aws iam create-role \
  --role-name BadgeSmith-GitHubActions-Role \
  --assume-role-policy-document file://github-oidc-trust-policy.json \
  --description "Role for BadgeSmith GitHub Actions deployment"

# Attach power user policy (development)
aws iam attach-role-policy \
  --role-name BadgeSmith-GitHubActions-Role \
  --policy-arn arn:aws:iam::aws:policy/PowerUserAccess

# Get role ARN
aws iam get-role --role-name BadgeSmith-GitHubActions-Role --query 'Role.Arn' --output text
```
