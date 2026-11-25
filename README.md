# Submarine Tracker

[SubmarineTracker](https://github.com/Infiziert90/SubmarineTracker) 的台服移植版。追蹤與管理公會潛水艇的插件，支援多間公會、多名角色。

## 功能

- 追蹤器：顯示目前配置、航行路線、返航時間
- 配置器：協助優化配置、規劃升級路線、提升經驗效率
- 可設定的返航／維修聊天提醒；返航時可另發 Windows 系統通知（預設關閉，只在遊戲失焦時；氣球通知透過 NotificationMaster，沒裝時只閃工作列圖示）或 Discord Webhook
- 速查（Helpy）：進展與倉庫存量一覽
- 自訂戰利品總覽：歷史紀錄、分析，可匯出 CSV
- 多組疊加視窗，方便查看下一趟航行、航線、解鎖與返航倒數
- 伺服器資訊列：左鍵開關主視窗，右鍵開關設定視窗
- 航線旁的按鈕可把這條航線複製成 AutoRetainer 的潛水艇點位計畫（只放進剪貼簿）
- 撈寶資料上傳到原作者的統計服務：預設關閉，要在設定裡自己勾選

## 指令

- `/stracker`：開啟追蹤器
- `/sbuilder`：開啟配置器
- `/sloot`：開啟戰利品總覽（`/sloot copy` 依目前設定將歷史紀錄匯出為 CSV 至剪貼簿）
- `/shelpy`：開啟速查視窗
- `/sconf`：開啟設定
- `/soverlay`：開啟／關閉返航疊加層

## 台服版的修改

- 介面繁體中文化（台服客戶端也載得到繁中語系）。
- 撈寶資料上傳改成預設關閉。
- 新增返航時的 Windows 系統通知。
- 新增「複製成 AutoRetainer 點位計畫」按鈕。
- 開放「這件物品會在哪幾個海域撈到」的查詢 IPC（`SubmarineTracker.GetSectorsForItem` 等）。
- 遊戲改版後航行結果的函式位址沒解出時，只停掉戰利品記錄，插件照常載入。
- 修正多個視窗在缺少公會資料時變成錯誤訊息。
- 配置器升級分頁計算中途出錯時，改在分頁顯示「計算失敗」，不再讓遊戲結束。
- 修正潛水艇資料庫被多條執行緒同時讀寫，避免偶爾讀不到資料或寫入失敗。
- 修正把所有公會都設成隱藏後資訊列的數字停住不更新，以及返航疊加層關著時潛水艇全部返航、下次打開不會自動展開公會的問題。
- 多處降低記憶體用量；插件卸載或更新時不再讓遊戲卡一下。

## 安裝

1. 在遊戲內輸入 `/xlsettings` 開啟「Dalamud 設定」，到「試驗性功能」分頁的「第三方插件倉庫」加入
   `https://raw.githubusercontent.com/ffxiv-tc-port/DalamudPluginsTC/main/repo.json`
   並啟用、儲存。
2. 輸入 `/xlplugins` 開啟「插件管理員」，搜尋「Submarine Tracker」安裝。

同一個插件只裝這裡的版本，不要同時裝上游版。

## 需要搭配的插件

- 選用：[NotificationMaster](https://github.com/ffxiv-tc-port/NotificationMaster)（返航時的 Windows 氣球通知）、[AutoRetainer](https://github.com/ffxiv-tc-port/AutoRetainer)（貼上複製出來的潛水艇點位計畫）

原作者：[Infiziert90](https://github.com/Infiziert90/SubmarineTracker)

授權：MIT（見 LICENSE）
