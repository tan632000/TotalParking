import re

file_path = 'c:/Users/HOME/source/repos/TotalParking/TotalParking/Web.config'

with open(file_path, 'r', encoding='utf-8') as f:
    content = f.read()

# Replace <system.web> block to include globalization tag
target = """  <system.web>
    <compilation debug="true" targetFramework="4.5" />
    <httpRuntime targetFramework="4.5" />
  </system.web>"""

replacement = """  <system.web>
    <compilation debug="true" targetFramework="4.5" />
    <httpRuntime targetFramework="4.5" />
    <globalization requestEncoding="utf-8" responseEncoding="utf-8" fileEncoding="utf-8" />
  </system.web>"""

if target in content:
    new_content = content.replace(target, replacement)
    with open(file_path, 'w', encoding='utf-8') as f:
        f.write(new_content)
    print("Web.config updated successfully!")
else:
    # Try with different line endings
    target_lf = target.replace('\r\n', '\n')
    replacement_lf = replacement.replace('\r\n', '\n')
    if target_lf in content:
        new_content = content.replace(target_lf, replacement_lf)
        with open(file_path, 'w', encoding='utf-8') as f:
            f.write(new_content)
        print("Web.config updated successfully! (LF)")
    else:
        print("Could not find the target section in Web.config")
