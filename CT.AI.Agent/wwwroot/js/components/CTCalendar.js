export function initializePikaday(elementId, dotNetHelper) {
    const input = document.getElementById(elementId);
    const picker = new Pikaday({
        field: input,
        format: 'yyyy/MM/dd',
        onSelect: function () {
            const date = this.getDate();
            // 生成本地日期字符串
            const year = date.getFullYear();
            const month = String(date.getMonth() + 1).padStart(2, '0');
            const day = String(date.getDate()).padStart(2, '0');
            const dateStr = `${year}/${month}/${day}`;
            dotNetHelper.invokeMethodAsync('UpdateDate', dateStr);
        }
    });
    input.pikaday = picker;
}
export function destroyPikaday(elementId) {
    const input = document.getElementById(elementId);
    if (input.pikaday) {
        input.pikaday.destroy();
        input.pikaday = null;
    }
}