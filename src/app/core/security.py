# File: src/app/core/security.py
from passlib.context import CryptContext

# Uses bcrypt for all new passwords created in this Python service.
# NOTE: Passwords hashed by the original .NET PasswordHasher (PBKDF2-SHA256)
# are NOT compatible with bcrypt. Existing users will need to reset their
# passwords after migrating to this Python backend.
pwd_context = CryptContext(schemes=["bcrypt"], deprecated="auto")


def hash_password(plain_password: str) -> str:
    return pwd_context.hash(plain_password)


def verify_password(plain_password: str, hashed_password: str) -> bool:
    try:
        return pwd_context.verify(plain_password, hashed_password)
    except Exception:
        return False
