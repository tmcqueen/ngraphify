const fs = require('fs');
import { Parser } from './parser';

class EventEmitter {
    constructor() {
        this.listeners = {};
    }

    on(event, callback) {
        this.listeners[event] = callback;
    }

    emit(event, data) {
        const cb = this.listeners[event];
        if (cb) cb(data);
    }
}

function createServer(config) {
    const emitter = new EventEmitter();
    emitter.on('request', handleRequest);
    return emitter;
}

function handleRequest(req) {
    fs.readFileSync(req.path);
}

module.exports = { EventEmitter, createServer };
