#!/usr/bin/env python3
"""
Portfolio Manager AI Evaluation Setup

This script sets up the Python evaluation environment for testing 
Portfolio Manager's AI capabilities.
"""

import subprocess
import sys
import os
from pathlib import Path

def run_command(cmd, description):
    """Run a command and handle errors."""
    print(f"📦 {description}...")
    try:
        result = subprocess.run(cmd, shell=True, check=True, capture_output=True, text=True)
        print(f"✅ {description} completed successfully")
        return True
    except subprocess.CalledProcessError as e:
        print(f"❌ {description} failed:")
        print(f"   Command: {cmd}")
        print(f"   Error: {e.stderr}")
        return False

def main():
    """Setup the evaluation environment."""
    
    print("🚀 Setting up Portfolio Manager AI Evaluation Environment")
    print("=" * 60)
    
    # Check Python version
    python_version = sys.version_info
    if python_version < (3, 8):
        print(f"❌ Python 3.8+ required, but found {python_version.major}.{python_version.minor}")
        return False
    
    print(f"✅ Python {python_version.major}.{python_version.minor}.{python_version.micro} detected")
    
    # Get the evaluation directory
    eval_dir = Path(__file__).parent
    os.chdir(eval_dir)
    
    print(f"📁 Working directory: {eval_dir}")
    
    # Create virtual environment (optional but recommended)
    venv_choice = input("\n🤔 Create a virtual environment? [Y/n]: ").strip().lower()
    
    if venv_choice in ['', 'y', 'yes']:
        if not run_command("python -m venv venv", "Creating virtual environment"):
            return False
        
        # Activate virtual environment
        if sys.platform == "win32":
            activate_cmd = r".\venv\Scripts\activate && "
        else:
            activate_cmd = "source venv/bin/activate && "
        
        print("💡 Virtual environment created. To activate it:")
        if sys.platform == "win32":
            print("   .\\venv\\Scripts\\activate")
        else:
            print("   source venv/bin/activate")
    else:
        activate_cmd = ""
    
    # Install requirements
    install_cmd = f"{activate_cmd}pip install -r requirements.txt"
    if not run_command(install_cmd, "Installing Python packages"):
        print("\n⚠️  Package installation failed. You can try manually:")
        print(f"   {install_cmd}")
        return False
    
    print("\n🎉 Setup completed successfully!")
    print("\n📋 Next steps:")
    print("1. Configure Azure OpenAI settings:")
    print("   - Set AZURE_OPENAI_ENDPOINT environment variable")
    print("   - Set AZURE_OPENAI_DEPLOYMENT environment variable")
    print("   - Ensure Azure credentials are configured")
    print("")
    print("2. Start your Portfolio Manager API:")
    print("   - Run: dotnet run (from API project)")
    print("   - Or: docker-compose up")
    print("   - Verify it's running at http://localhost:5000")
    print("")
    print("3. Run the evaluation:")
    print("   - python portfolio_evaluator.py")
    print("")
    print("🔧 Environment variables you can set:")
    print("   - PORTFOLIO_API_URL (default: http://localhost:5000)")
    print("   - TEST_ACCOUNT_ID (default: 1)")
    print("   - AZURE_OPENAI_ENDPOINT")
    print("   - AZURE_OPENAI_DEPLOYMENT")
    
    return True

if __name__ == "__main__":
    success = main()
    sys.exit(0 if success else 1)