export async function refreshList() {
    try {
        const response = await fetch("/AutoCmd/list");
        if (!response.ok)
            throw new Error("HTTP error");
        const list = await response.json();

        const select = document.getElementById("autoList");
        select.innerHTML = "";

        for (const item of list) {
            const option = document.createElement("option");
            option.value = item.id;
            option.textContent = item.name;
            select.appendChild(option);
        }
    }
    catch (err) {
        console.error("Failed to load list:", err);
    }
}

export function HTML(target) {
    target.innerHTML = `
<h2>Auto Command Groups</h2>
<div class="Menu"></div>
<label for="autoList">AutoCommands: </label><select id="autoList"></select>
<button onclick="refreshList()">Refresh</button>
    `;
}