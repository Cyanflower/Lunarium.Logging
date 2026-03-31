import os
from pathlib import Path
import tiktoken
from collections import defaultdict

# --- 配置区 ---
TARGET_EXTENSION = ".cs"
EXCLUDE_DIRS = {"obj", "bin", ".git", ".vs"}
MODEL_ENCODING = "o200k_base" # 对应 GPT-4o 级别的高效分词器

# Get project root relative to this script (scripts/token_analyzer.py -> project_root)
PROJECT_ROOT = Path(__file__).resolve().parent.parent
OUTPUT_FILE = PROJECT_ROOT / "code_token_report.md"

def get_token_count(text, encoding):
    """计算文本的 token 数量"""
    return len(encoding.encode(text))

def analyze_project():
    # 初始化分词器
    try:
        enc = tiktoken.get_encoding(MODEL_ENCODING)
    except Exception:
        enc = tiktoken.get_encoding("cl100k_base") # 备用方案

    root_path = PROJECT_ROOT
    file_stats = {}      # 存储文件路径 -> token数
    dir_totals = defaultdict(int) # 存储目录路径 -> 该目录下所有子项总和

    print(f"🚀 开始扫描目录: {root_path}")
    print(f"🔍 排除目录: {', '.join(EXCLUDE_DIRS)}")

    # 1. 遍历并计算单个文件
    for path in root_path.rglob(f"*{TARGET_EXTENSION}"):
        # 检查路径中是否包含要排除的目录
        if any(exclude in path.parts for exclude in EXCLUDE_DIRS):
            continue
        
        try:
            content = path.read_text(encoding="utf-8")
            count = get_token_count(content, enc)
            
            # 记录文件数据 (相对于根目录的路径)
            rel_path = path.relative_to(root_path)
            file_stats[rel_path] = count
            
            # 2. 向上累加到所有父目录
            for parent in rel_path.parents:
                dir_totals[parent] += count
                
        except Exception as e:
            print(f"⚠️ 无法读取文件 {path}: {e}")

    # 3. 生成 Markdown 报告
    with open(OUTPUT_FILE, "w", encoding="utf-8") as f:
        f.write(f"# 代码库 Token 分析报告\n\n")
        f.write(f"- **总 Token 数量**: `{dir_totals[Path('.')]:,}`\n")
        f.write(f"- **统计范围**: 所有 `{TARGET_EXTENSION}` 文件\n")
        f.write(f"- **排除规则**: `{', '.join(EXCLUDE_DIRS)}` \n\n")
        f.write("--- \n\n")
        f.write("## 目录与文件结构详情\n\n")
        
        # 获取所有涉及的路径并排序，确保树形生成逻辑正确
        all_paths = sorted(list(file_stats.keys()) + list(dir_totals.keys()))
        
        for p in all_paths:
            indent = "  " * len(p.parts)
            name = p.name if p.name else "."
            
            if p in dir_totals:
                # 如果是目录
                token_sum = dir_totals[p]
                if token_sum > 0: # 仅显示包含有.cs文件的目录
                    f.write(f"{indent}- 📁 **{name}/** *(Total: `{token_sum:,}`)* \n")
            else:
                # 如果是文件
                token_count = file_stats[p]
                f.write(f"{indent}- 📄 {name}  (`{token_count:,}`)\n")

    print(f"✅ 分析完成！报告已生成至: {OUTPUT_FILE}")

if __name__ == "__main__":
    analyze_project()