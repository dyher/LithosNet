// MMORPG 標準 AOI 標頭檔
#ifndef _AOI_H_
#define _AOI_H_

// 當有其他實體進入你的視野時，Driver 會自動呼叫此 Apply
void aoi_enter(string who, int x, int y);

// 當有其他實體離開你的視野時，Driver 會自動呼叫此 Apply
void aoi_leave(string who);

#endif
