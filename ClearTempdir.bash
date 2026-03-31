#!/bin/bash

# 定义要删除的目标
TARGETS="bin obj"

echo "--- 准备清理 .NET 临时目录 ---"

# 1. 先列出所有将要删除的目录，让用户确认
echo "以下目录将被删除："
for target in $TARGETS; do
    find . -type d -name "$target"
done

echo "----------------------------"
read -p "确定要删除以上所有目录吗？(y/n): " confirm

if [ "$confirm" == "y" ]; then
    for target in $TARGETS; do
        find . -type d -name "$target" -exec rm -rf {} +
        echo "已清理所有 $target 目录。"
    done
    echo "Done."
else
    echo "操作已取消。"
fi