document.addEventListener("DOMContentLoaded", function () {
    // Keep browser validation messages in English regardless of browser language.
    const validationMessages = {
        Server_Name: "Enter a server name.",
        Server_ApiAddress: "Enter an API address.",
        WriteServerName: "Select a test server.",
        NodeId: "Select a node.",
        DataType: "Select a data type.",
        Value: "Enter a value."
    };
    for (const field of document.querySelectorAll("input[required], select[required]")) {
        field.addEventListener("invalid", () => {
            field.setCustomValidity(field.validity.valueMissing
                ? validationMessages[field.id] || "Complete this field."
                : "Enter a valid value.");
        });
        const clearValidation = () => field.setCustomValidity("");
        field.addEventListener("input", clearValidation);
        field.addEventListener("change", clearValidation);
    }

    const serverSelect = document.getElementById("WriteServerName");
    const nodeSelect = document.getElementById("NodeId");
    const searchInput = document.getElementById("NodeSearch");
    const status = document.getElementById("NodeSearchStatus");
    if (!serverSelect || !nodeSelect || !searchInput || !status) return;

    const firstServer = Array.from(serverSelect.options)
        .find(option => option.value && !option.disabled);
    if (firstServer) serverSelect.value = firstServer.value;

    const modeSelect = document.getElementById("ServerMode");
    const modeServer = document.getElementById("ModeServerName");
    const scenarioField = document.getElementById("ScenarioPathField");
    function updateScenarioField() {
        if (scenarioField && modeSelect) scenarioField.hidden = modeSelect.value !== "Scenario";
    }
    function syncModeServer() {
        if (!modeSelect || !modeServer) return;
        const selected = serverSelect.selectedOptions[0];
        modeServer.value = serverSelect.value;
        const mode = selected?.dataset.mode || "";
        if (mode) modeSelect.value = mode;
        document.getElementById("ModeServerLabel").textContent = serverSelect.value || "no server selected";
        document.getElementById("CurrentServerMode").textContent =
            mode ? "Current: " + mode : "Current mode unavailable";
        document.getElementById("ApplyServerMode").disabled = !serverSelect.value;
        updateScenarioField();
    }
    if (modeSelect) modeSelect.addEventListener("change", updateScenarioField);
    serverSelect.addEventListener("change", syncModeServer);
    syncModeServer();

    // An empty search keeps the complete list for the selected server.
    const nodes = Array.from(nodeSelect.options)
        .filter(option => option.dataset.server)
        .map(option => ({
            id: option.value,
            label: option.textContent,
            server: option.dataset.server,
            searchable: option.textContent.toLocaleLowerCase("en")
        }));

    function filterNodes() {
        nodeSelect.setCustomValidity("");
        const selectedId = nodeSelect.value;
        const server = serverSelect.value;
        const terms = searchInput.value.trim().toLocaleLowerCase("en")
            .split(/\s+/).filter(Boolean);
        const serverNodes = nodes.filter(node => node.server === server);
        const matches = serverNodes.filter(node =>
            terms.every(term => node.searchable.includes(term)));

        nodeSelect.replaceChildren(new Option(
            !server ? "Select a test server first" :
            matches.length ? "Select a node" : "No matching nodes found", ""));
        for (const node of matches) {
            nodeSelect.add(new Option(node.label, node.id));
        }
        if (matches.some(node => node.id === selectedId)) nodeSelect.value = selectedId;
        searchInput.disabled = !server;
        nodeSelect.disabled = !server || matches.length === 0;
        status.textContent = !server ? "Select a test server first." :
            terms.length ? matches.length + " of " + serverNodes.length + " nodes found" :
            serverNodes.length + " nodes · browse the list or use search";
    }

    serverSelect.addEventListener("change", function () {
        searchInput.value = "";
        nodeSelect.value = "";
        filterNodes();
    });
    searchInput.addEventListener("input", filterNodes);
    const clearSearch = document.getElementById("NodeSearchClear");
    if (clearSearch) {
        clearSearch.addEventListener("click", function () {
            searchInput.value = "";
            filterNodes();
            nodeSelect.focus();
        });
    }
    searchInput.addEventListener("keydown", function (event) {
        if (event.key !== "Enter") return;
        event.preventDefault();
        if (nodeSelect.options.length === 2 && nodeSelect.options[1].value) {
            nodeSelect.value = nodeSelect.options[1].value;
            nodeSelect.dispatchEvent(new Event("change", { bubbles: true }));
            nodeSelect.focus();
        }
    });
    filterNodes();
});



