#include <stdio.h>

// 【C 層】極致效能的傷害計算公式 (模擬 rAthena 的底層 C 計算)
// 傷害 = (攻擊 - 防禦) * 1.5 倍暴擊係數
int calc_damage(int atk, int def) {
    int base_dmg = atk - def;
    if (base_dmg < 1) base_dmg = 1; // 保底 1 點傷害
    
    // 使用整數運算模擬 1.5 倍，避免浮點數效能損耗
    return (base_dmg * 150) / 100; 
}
