window.capOfflineOutbox = (function () {
  const DB_NAME = 'cap-pos-outbox';
  const STORE = 'checkouts';
  const MAX_AGE_MS = 24 * 60 * 60 * 1000; // 24h
  const MAX_ITEMS = 100;

  function openDb() {
    return new Promise((resolve, reject) => {
      const req = indexedDB.open(DB_NAME, 1);
      req.onupgradeneeded = () => {
        const db = req.result;
        if (!db.objectStoreNames.contains(STORE)) {
          const os = db.createObjectStore(STORE, { keyPath: 'idempotencyKey' });
          os.createIndex('status', 'status', { unique: false });
          os.createIndex('createdAt', 'createdAt', { unique: false });
        }
      };
      req.onsuccess = () => resolve(req.result);
      req.onerror = () => reject(req.error);
    });
  }

  function txDone(tx) {
    return new Promise((resolve, reject) => {
      tx.oncomplete = () => resolve();
      tx.onerror = () => reject(tx.error);
      tx.onabort = () => reject(tx.error || new Error('aborted'));
    });
  }

  async function enqueue(item) {
    const db = await openDb();
    const all = await listAll(db);
    const pending = all.filter(x => x.status === 'Pending' || x.status === 'Failed' || x.status === 'Syncing');
    if (pending.length >= MAX_ITEMS) throw new Error('Offline queue full (max ' + MAX_ITEMS + ')');

    const row = {
      idempotencyKey: item.idempotencyKey,
      payload: item.payload,
      status: 'Pending',
      createdAt: item.createdAt || new Date().toISOString(),
      retryCount: 0,
      lastError: null,
      shiftId: item.shiftId || null
    };
    const tx = db.transaction(STORE, 'readwrite');
    tx.objectStore(STORE).put(row);
    await txDone(tx);
    return row;
  }

  function listAll(db) {
    return new Promise((resolve, reject) => {
      const tx = db.transaction(STORE, 'readonly');
      const req = tx.objectStore(STORE).getAll();
      req.onsuccess = () => resolve(req.result || []);
      req.onerror = () => reject(req.error);
    });
  }

  async function list() {
    const db = await openDb();
    const all = await listAll(db);
    const now = Date.now();
    return all
      .filter(x => {
        const age = now - Date.parse(x.createdAt || 0);
        return age <= MAX_AGE_MS;
      })
      .sort((a, b) => Date.parse(a.createdAt) - Date.parse(b.createdAt));
  }

  async function pendingCount() {
    const items = await list();
    return items.filter(x => x.status === 'Pending' || x.status === 'Failed' || x.status === 'Syncing').length;
  }

  async function update(idempotencyKey, patch) {
    const db = await openDb();
    const tx = db.transaction(STORE, 'readwrite');
    const store = tx.objectStore(STORE);
    const getReq = store.get(idempotencyKey);
    const existing = await new Promise((resolve, reject) => {
      getReq.onsuccess = () => resolve(getReq.result);
      getReq.onerror = () => reject(getReq.error);
    });
    if (!existing) {
      await txDone(tx);
      return null;
    }
    Object.assign(existing, patch);
    store.put(existing);
    await txDone(tx);
    return existing;
  }

  async function remove(idempotencyKey) {
    const db = await openDb();
    const tx = db.transaction(STORE, 'readwrite');
    tx.objectStore(STORE).delete(idempotencyKey);
    await txDone(tx);
  }

  async function drainReady() {
    const items = await list();
    return items.filter(x => x.status === 'Pending' || x.status === 'Failed');
  }

  return { enqueue, list, pendingCount, update, remove, drainReady, MAX_AGE_MS, MAX_ITEMS };
})();
