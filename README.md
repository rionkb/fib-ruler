# Fib Ruler

Windows上のあらゆるチャートへ重ねて使える、軽量なフィボナッチ・オーバーレイ定規です。
特定のチャートサービスやAPIに依存せず、画面座標だけを使って動作します。

## 特徴

- TradingView等の特定サービスに依存しない
- Windows上の任意のチャートへ重ねて表示
- Fib Overlayは透明・常に最前面（ControlWindowは通常のウィンドウ）
- 2点を基準にFibラインを表示
- 0～4.236の標準Fibと1.0以上のExtension
- A/B個別ドラッグ、定規全体ドラッグ
- EDIT / LOCK、LOCK時のクリック透過
- `Ctrl + Alt + F` でEDIT/LOCKを切り替え
- `Shift + A` でDraw Modeを開始
- `Shift + D` で配置済みFibを削除
- 起動時はControlWindowを表示し、ToolはOFFで開始
- ControlWindowのON/OFFボタンでFibの表示・非表示を切り替え
- FibレベルのON/OFF
- 任意Fibレベルの追加・削除
- 配置済みFibの小さい削除ボタン（LOCK時は非表示）
- A/B位置およびFib設定の自動保存
- API・OCR・画像認識不要

## 基本操作

### 起動時

起動するとControlWindowが表示され、ToolはOFFで開始します。OFFはFibの削除ではなく非表示であり、ONに戻すと既存Fibを同じ位置へ復元します。

### ショートカット

- ControlWindowの `Enable Overlay` / `Disable Overlay`: Overlay ON / OFF
- `Shift + A`: Fib描画開始
- 左ドラッグ: Fib描画
- `Shift + D`: 現在のFibを削除
- `Esc`: 描画キャンセル
- `Ctrl + Alt + F`: EDIT / LOCK
- 右クリック: Fib設定

### Fibを新しく描く

1. `Shift + A` でDraw ModeをONにします。
2. 左ドラッグします。
3. ドラッグ開始位置が1点目（1.000）、マウスを離した位置が2点目（0.000）です。
4. Fibが確定するとDraw Modeは自動的にOFFになります。

Draw Mode中はFib Rulerがマウス入力を受け取り、通常の左ドラッグでFibを描画します。

### 描画キャンセル

描画途中に `Esc` を押すと、その描画をキャンセルできます。確定済みFibがある場合は保持されます。

### 再描画

`Shift + A` → 左ドラッグで、既存Fibを新しいFibへ置き換えます。描画確定後はDraw Modeが自動的にOFFになり、現時点では複数Fibの同時表示には対応していません。

### 端点移動

2つのリングをドラッグします。

### 定規全体移動

2点を結ぶ斜め基準線をドラッグします。

### Fib設定

端点または基準線を右クリックします。FibレベルのON/OFF、追加、削除を操作できます。

### LOCK

`Ctrl + Alt + F` を押します。

LOCK中はFib表示を維持したまま、背後のブラウザ、TradingView、証券会社アプリなどを操作できます。
もう一度 `Ctrl + Alt + F` を押すとEDITへ戻ります。

### ControlWindow / Settings

ControlWindowのSettingsからDraw、Deleteのショートカットを変更できます。選択したshortcutは保存されますが、ToolのON/OFF状態は保存されず、次回起動時は毎回OFFで開始します。

SettingsではTradingView風の標準色グループとFib帯の透明度も変更できます。Reset to TradingView Colorsで既定値へ戻せます。

ControlWindowには現在の描画操作として `Draw: Shift + A`、削除操作として `Delete: Shift + D` を表示します。ショートカットを変更すると表示も更新されます。

## 標準Fibレベル

```text
0.000
0.236
0.382
0.500
0.618
0.786
1.000
1.272
1.414
1.618
2.000
2.618
3.618
4.236
```

任意のFibレベルも追加できます。

## 設定保存

設定は `%AppData%\FibRuler\settings.json` に保存されます。

- 端点位置
- Fibレベル
- FibレベルのON/OFF

## 動作環境

- Windows 10 / 11
- x64

self-contained配布版では.NET Runtimeを別途インストールする必要はありません。
Windows 10での実機検証はまだ行っていません。

## インストール / 起動

インストーラーはありません。配布された `FibRuler.exe` を起動してください。

コード署名を行っていないため、Windows SmartScreen等で警告が表示される場合があります。

## アンインストール

アプリを終了して `FibRuler.exe` を削除してください。設定も削除する場合のみ `%AppData%\\FibRuler` を削除します。

## 現在のバージョン

v1.0.0-rc.1

## 注意事項

- 株価データを取得しません
- 表示位置は画面座標を基準にします
- 投資判断を行うアプリではありません

## License

License: Not specified yet
