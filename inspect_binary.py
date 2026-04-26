with open('YSM_Models_Pool/GI_Furina.ysm', 'rb') as f:
    content = f.read()

# Find double newline or null byte
import re
match = re.search(rb'\r?\n\r?\n[^\r\n< -]', content[100:])
if match:
    pos = 100 + match.start()
    print("Potential transition at:", pos)
    print("Bytes:", content[pos:pos+50].hex())

# Search for PK zip header (50 4B 03 04) anywhere in file
pk = content.find(b'PK\x03\x04')
print("PK zip at:", pk)

# Search for any PNG header (89 50 4E 47)
png = content.find(b'\x89PNG')
print("PNG at:", png)

# Search for 7z (37 7A BC AF)
sz = content.find(b'7z\xbc\xaf')
print("7z at:", sz)
