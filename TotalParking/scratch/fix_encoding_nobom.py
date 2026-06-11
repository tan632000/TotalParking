import codecs

file_path = 'c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/ZoneDetail.cshtml'

# Read the file content as UTF-8 (handling potential BOM)
with codecs.open(file_path, 'r', 'utf-8-sig') as f:
    content = f.read()

# Write the file back with plain UTF-8 (without BOM)
with open(file_path, 'w', encoding='utf-8') as f:
    f.write(content)

print("Successfully converted ZoneDetail.cshtml to UTF-8 without BOM")
