import re
import os

files = [
    'c:/OnlineExamWeb/OnlineExam/OnlineExam/Views/Student/Index.cshtml',
    'c:/OnlineExamWeb/OnlineExam/OnlineExam/Views/Student/JoinClass.cshtml',
    'c:/OnlineExamWeb/OnlineExam/OnlineExam/Views/Student/MyExams.cshtml',
    'c:/OnlineExamWeb/OnlineExam/OnlineExam/Views/Student/Results.cshtml'
]

for file in files:
    with open(file, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # Store the model directive
    model_match = re.search(r'@model\s+[^\n]+', content)
    model_directive = model_match.group(0) + "\n" if model_match else ""
    if model_match:
        content = content.replace(model_match.group(0), '')

    # Remove Layout = null;
    content = re.sub(r'@\{\s*Layout\s*=\s*null;\s*\}', '', content)
    
    # Remove HTML skeleton up to Main Content Area or the body start
    if '<!-- Main Content Area -->' in content:
        content = re.sub(r'<!DOCTYPE html>.*?<!-- Main Content Area -->', '', content, flags=re.DOTALL)
        # Remove the wrappers:
        content = re.sub(r'<div class="flex-1 flex flex-col md:ml-64 min-h-screen">\s*<div class="flex-1 w-full max-w-7xl mx-auto p-6 md:p-10 flex flex-col">', '<div class="w-full max-w-7xl mx-auto flex flex-col">', content)
        # Remove header
        content = re.sub(r'<header class="flex justify-between items-center mb-10 w-full">.*?</header>', '', content, flags=re.DOTALL)
        # Remove footer and closing divs
        content = re.sub(r'<footer.*?</footer>\s*</div>\s*</div>', '</div>', content, flags=re.DOTALL)
        # Remove mobile nav
        content = re.sub(r'<!-- Mobile Nav -->.*?</nav>', '', content, flags=re.DOTALL)
        content = re.sub(r'<nav class="md:hidden.*?</nav>', '', content, flags=re.DOTALL)
    else:
        # For JoinClass and MyExams
        content = re.sub(r'<!DOCTYPE html>.*?<body.*?>', '', content, flags=re.DOTALL)
    
    # Extract scripts
    scripts = re.findall(r'<script.*?</script>', content, flags=re.DOTALL)
    for s in scripts:
        content = content.replace(s, '')
        
    scripts_str = '\n'.join(scripts)
    
    content = re.sub(r'</body>\s*</html>', '', content)
    
    final_content = model_directive + content.strip()
    if scripts_str.strip():
        final_content += f'\n@section Scripts {{\n    {scripts_str}\n}}'
        
    with open(file, 'w', encoding='utf-8') as f:
        f.write(final_content)
