/**
 * recordEditor/richtext.js
 *
 * Quill editor init, HTML detection, rich-text helpers.
 * Exportuje:
 * - looksLikeHtml
 * - getOrCreateRichTextSourceContainer
 * - initRichTextEditors
 * - setRecordEditorRichTextValue
 */

export function looksLikeHtml(value) {
    return /<\s*\/?\s*[a-z][^>]*>/i.test(value || "");
}

export function getOrCreateRichTextSourceContainer(form) {
    if (!(form instanceof HTMLFormElement)) {
        return null;
    }

    const existing = form.querySelector("[data-rich-text-source-container]");
    if (existing instanceof HTMLElement) {
        return existing;
    }

    const container = document.createElement("div");
    container.className = "richtext-source-container";
    container.setAttribute("data-rich-text-source-container", "true");
    container.setAttribute("aria-hidden", "true");
    form.appendChild(container);
    return container;
}

export function initRichTextEditors(scope) {
    if (!(scope instanceof HTMLElement || scope instanceof Document)) {
        return;
    }

    if (typeof window.Quill !== "function") {
        return;
    }

    const quillCtor = window.Quill;
    scope.querySelectorAll("textarea[data-rich-text='true']").forEach((textarea) => {
        if (!(textarea instanceof HTMLTextAreaElement)
            || textarea.disabled
            || textarea.dataset.richTextReady === "true") {
            return;
        }

        const host = document.createElement("div");
        host.className = "richtext-host";
        textarea.insertAdjacentElement("beforebegin", host);
        const form = textarea.closest("form");
        const labelParent = textarea.closest("label");
        if (labelParent instanceof HTMLLabelElement && form instanceof HTMLFormElement) {
            const sourceContainer = getOrCreateRichTextSourceContainer(form);
            if (sourceContainer instanceof HTMLElement) {
                sourceContainer.appendChild(textarea);
            } else {
                host.appendChild(textarea);
            }
        } else {
            host.appendChild(textarea);
        }
        textarea.classList.add("richtext-source-hidden");
        textarea.setAttribute("aria-hidden", "true");
        textarea.setAttribute("tabindex", "-1");

        const editorShell = document.createElement("div");
        editorShell.className = "richtext-editor-shell";
        host.appendChild(editorShell);

        const placeholder = (textarea.getAttribute("placeholder") || "").trim();
        const quill = new quillCtor(editorShell, {
            theme: "snow",
            placeholder,
            modules: {
                toolbar: [
                    ["bold", "italic", "underline"],
                    ["link"],
                    [{ list: "ordered" }, { list: "bullet" }],
                    [{ indent: "-1" }, { indent: "+1" }]
                ]
            }
        });

        textarea.dataset.richTextReady = "true";
        textarea._richTextEditor = quill;

        const editorNode = editorShell.querySelector(".ql-editor");
        const configuredMinHeight = Number.parseFloat((textarea.dataset.richTextMinHeight || "").trim());
        const minHeight = Number.isFinite(configuredMinHeight)
            ? configuredMinHeight
            : Math.max(88, Number.parseInt(textarea.getAttribute("rows") || "4", 10) * 22);
        if (editorNode instanceof HTMLElement) {
            editorNode.style.minHeight = `${Math.round(minHeight)}px`;
        }

        const syncTextarea = () => {
            const text = (quill.getText() || "").replace(/\u00a0/g, " ").trim();
            if (!text) {
                textarea.value = "";
                return;
            }

            const html = (quill.root?.innerHTML || "").trim();
            textarea.value = html && html !== "<p><br></p>" ? html : "";
        };

        const resize = () => {
            if (!(editorNode instanceof HTMLElement)) {
                return;
            }

            editorNode.style.height = "auto";
            const nextHeight = Math.max(editorNode.scrollHeight, minHeight);
            editorNode.style.height = `${Math.round(nextHeight)}px`;
        };

        const initialValue = textarea.value || "";
        if (initialValue.trim().length > 0) {
            if (looksLikeHtml(initialValue)) {
                quill.clipboard.dangerouslyPasteHTML(initialValue);
            } else {
                quill.setText(initialValue);
            }
        } else {
            quill.setText("");
        }

        if (form instanceof HTMLFormElement) {
            form.addEventListener("submit", syncTextarea);
        }

        quill.on("text-change", () => {
            syncTextarea();
            resize();
        });

        quill.on("editor-change", () => {
            resize();
        });

        syncTextarea();
        window.requestAnimationFrame(resize);
    });
}

export function setRecordEditorRichTextValue(textarea, nextValue) {
    if (!(textarea instanceof HTMLTextAreaElement)) {
        return;
    }

    const normalized = String(nextValue ?? "");
    textarea.value = normalized;
    const editor = textarea._richTextEditor;
    if (!editor) {
        return;
    }

    if (!normalized.trim()) {
        if (typeof editor.setText === "function") {
            editor.setText("");
        }
        return;
    }

    if (looksLikeHtml(normalized)
        && editor.clipboard
        && typeof editor.clipboard.dangerouslyPasteHTML === "function") {
        editor.clipboard.dangerouslyPasteHTML(normalized);
        return;
    }

    if (typeof editor.setText === "function") {
        editor.setText(normalized);
    }
}
