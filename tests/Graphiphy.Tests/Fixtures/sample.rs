use std::collections::HashMap;
use std::io::Read;

pub struct Config {
    pub port: u16,
    pub host: String,
}

pub trait Handler {
    fn handle(&self, request: &Request) -> Response;
}

pub struct Router {
    routes: HashMap<String, Box<dyn Handler>>,
}

impl Router {
    pub fn new() -> Self {
        Router { routes: HashMap::new() }
    }

    pub fn add_route(&mut self, path: &str, handler: Box<dyn Handler>) {
        self.routes.insert(path.to_string(), handler);
    }

    pub fn dispatch(&self, request: &Request) -> Response {
        match self.routes.get(&request.path) {
            Some(handler) => handler.handle(request),
            None => Response::not_found(),
        }
    }
}

fn main() {
    let mut router = Router::new();
    let config = Config { port: 8080, host: "localhost".to_string() };
    println!("Starting on {}:{}", config.host, config.port);
}
