package main

import (
	"fmt"
	"net/http"
)

type Server struct {
	Port   int
	Router *Router
}

type Router struct {
	routes map[string]http.HandlerFunc
}

func NewServer(port int) *Server {
	r := NewRouter()
	return &Server{Port: port, Router: r}
}

func NewRouter() *Router {
	return &Router{routes: make(map[string]http.HandlerFunc)}
}

func (s *Server) Start() error {
	addr := fmt.Sprintf(":%d", s.Port)
	return http.ListenAndServe(addr, nil)
}

func (r *Router) Handle(path string, handler http.HandlerFunc) {
	r.routes[path] = handler
}

func main() {
	s := NewServer(8080)
	s.Router.Handle("/", func(w http.ResponseWriter, r *http.Request) {
		fmt.Fprintf(w, "Hello")
	})
	s.Start()
}
