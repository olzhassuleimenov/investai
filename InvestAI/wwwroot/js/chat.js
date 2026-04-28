let activeChatId = null;
let selectedFile = null;

/* Загрузить историю чата */
async function loadChat(chatId) {
    activeChatId = chatId;

    document.querySelectorAll('.chat-item').forEach(el => el.classList.remove('active'));
    const activeBtn = document.querySelector(`.chat-item[onclick="loadChat('${chatId}')"]`);
    if (activeBtn) activeBtn.classList.add('active');

    if (activeBtn) {
        const title = activeBtn.querySelector('.chat-title')?.textContent || 'Чат';
        document.getElementById('chat-title').textContent = title;
    }

    const emptyState = document.getElementById('chat-empty-state');
    if (emptyState) emptyState.style.display = 'none';

    const list = document.getElementById('messages-list');
    list.innerHTML = '';

    try {
        const res = await fetch(`/Chat/Messages/${chatId}`);
        if (!res.ok) throw new Error('Ошибка загрузки');
        const msgs = await res.json();

        if (msgs.length === 0) {
            list.innerHTML = `
                <div class="chat-empty">
                    <div class="chat-empty-icon">💬</div>
                    <h3>Чат пуст</h3>
                    <p>Задайте вопрос про ваш портфель</p>
                </div>`;
        } else {
            msgs.forEach(m => appendMessage(m));
        }
    } catch (err) {
        list.innerHTML = `<div class="chat-empty"><p style="color:var(--color-red)">Ошибка загрузки чата</p></div>`;
    }

    scrollToBottom();
}

/* Отправить сообщение */
async function sendMessage() {
    const input = document.getElementById('chat-input');
    const text = input.value.trim();

    if (!activeChatId) {
        await createChatAndSend(text);
        return;
    }
    if (!text && !selectedFile) return;

    const sendBtn = document.getElementById('send-btn');
    sendBtn.disabled = true;
    input.value = '';
    input.style.height = 'auto';

    appendMessage({ role: 'user', text: text || `[Файл: ${selectedFile?.name}]`, createdAt: new Date().toISOString() });
    removeFile();
    showTyping();
    scrollToBottom();

    try {
        const res = await fetch('/Chat/SendMessage', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ chatId: activeChatId, text })
        });
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        const data = await res.json();
        hideTyping();
        appendMessage(data.aiMessage);
        if (data.aiMessage?.hasViz) {
            appendAnalyticsTag();
            refreshAnalytics();
        }
    } catch (err) {
        hideTyping();
        appendMessage({ role: 'assistant', text: 'Произошла ошибка. Попробуйте ещё раз.', createdAt: new Date().toISOString() });
    } finally {
        sendBtn.disabled = false;
        scrollToBottom();
    }
}

/* Создать новый чат */
async function createChat() {
    try {
        const res = await fetch('/Chat/Create', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ title: 'Новый чат' })
        });
        const chat = await res.json();
        addChatToSidebar(chat.id, chat.title);
        loadChat(chat.id);
    } catch (err) {
        console.error('Ошибка создания чата:', err);
    }
}

async function createChatAndSend(text) {
    if (!text) return;
    try {
        const res = await fetch('/Chat/Create', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ title: text.slice(0, 40) })
        });
        const chat = await res.json();
        addChatToSidebar(chat.id, chat.title);
        activeChatId = chat.id;
        document.getElementById('chat-title').textContent = chat.title;
        const emptyState = document.getElementById('chat-empty-state');
        if (emptyState) emptyState.style.display = 'none';
        const input = document.getElementById('chat-input');
        input.value = text;
        sendMessage();
    } catch (err) {
        console.error('Ошибка:', err);
    }
}

/* Добавить чат в сайдбар */
function addChatToSidebar(chatId, title) {
    const section = document.getElementById('chats-section');
    const placeholder = section.querySelector('div:not(.chats-label)');
    if (placeholder) placeholder.remove();

    if (!section.querySelector('.chats-label')) {
        const label = document.createElement('div');
        label.className = 'chats-label';
        label.textContent = 'Чаты';
        section.insertBefore(label, section.firstChild);
    }

    const btn = document.createElement('button');
    btn.className = 'chat-item';
    btn.setAttribute('onclick', `loadChat('${chatId}')`);
    btn.innerHTML = `
        <span class="chat-title">${escapeHtml(title)}</span>
        <span class="chat-date">сейчас</span>`;
    const label = section.querySelector('.chats-label');
    label.insertAdjacentElement('afterend', btn);
}

/* Рендер пузыря */
function appendMessage({ role, text, createdAt }) {
    const list = document.getElementById('messages-list');
    const empty = list.querySelector('.chat-empty');
    if (empty) empty.remove();

    const div = document.createElement('div');
    div.className = `message message-${role}`;
    const time = createdAt
        ? new Date(createdAt).toLocaleTimeString('ru', { hour: '2-digit', minute: '2-digit' })
        : '';
    div.innerHTML = `
        <div class="bubble">${escapeHtml(text)}</div>
        <span class="msg-time">${time}</span>`;
    list.appendChild(div);
}

function appendAnalyticsTag() {
    const list = document.getElementById('messages-list');
    const tag = document.createElement('div');
    tag.style.cssText = 'display:flex;justify-content:flex-start;margin-bottom:6px';
    tag.innerHTML = `<span class="msg-tag">📊 Аналитика обновлена</span>`;
    list.appendChild(tag);
}

/* Typing indicator */
function showTyping() {
    const list = document.getElementById('messages-list');
    const div = document.createElement('div');
    div.id = 'typing-indicator';
    div.className = 'typing-indicator';
    div.innerHTML = `
        <div class="typing-bubble">
            <div class="typing-dot"></div>
            <div class="typing-dot"></div>
            <div class="typing-dot"></div>
        </div>`;
    list.appendChild(div);
}
function hideTyping() {
    const el = document.getElementById('typing-indicator');
    if (el) el.remove();
}

/* Файл */
function handleFileSelect(input) {
    if (!input.files?.length) return;
    selectedFile = input.files[0];
    document.getElementById('file-preview-name').textContent = selectedFile.name;
    document.getElementById('file-preview').style.display = 'flex';
}
function removeFile() {
    selectedFile = null;
    const input = document.getElementById('file-input');
    if (input) input.value = '';
    const preview = document.getElementById('file-preview');
    if (preview) preview.style.display = 'none';
}

/* Обновить правую панель */
function refreshAnalytics() {
    setTimeout(() => {
        const url = new URL(window.location.href);
        if (activeChatId) url.searchParams.set('chatId', activeChatId);
        window.location.href = url.toString();
    }, 800);
}

/* Утилиты */
function scrollToBottom() {
    const list = document.getElementById('messages-list');
    if (list) list.scrollTop = list.scrollHeight;
}
function escapeHtml(text) {
    if (!text) return '';
    const div = document.createElement('div');
    div.appendChild(document.createTextNode(text));
    return div.innerHTML;
}

/* Enter → отправить */
document.addEventListener('DOMContentLoaded', function () {
    const input = document.getElementById('chat-input');
    if (!input) return;
    input.addEventListener('keydown', function (e) {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            sendMessage();
        }
    });
});