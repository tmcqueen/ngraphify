import { EventEmitter } from 'events';

interface Config {
    port: number;
    host: string;
}

class HttpServer extends EventEmitter {
    private config: Config;

    constructor(config: Config) {
        super();
        this.config = config;
    }

    listen(): void {
        this.emit('listening', this.config.port);
    }

    handleRequest(path: string): Response {
        return new Response(200, path);
    }
}

class Response {
    constructor(public status: number, public body: string) {}
}

export function createApp(config: Config): HttpServer {
    const server = new HttpServer(config);
    server.listen();
    return server;
}
