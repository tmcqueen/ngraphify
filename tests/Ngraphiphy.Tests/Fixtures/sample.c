#include <stdio.h>
#include <stdlib.h>
#include "utils.h"

typedef struct {
    int x;
    int y;
} Point;

typedef struct Node {
    int value;
    struct Node* next;
} Node;

void print_point(Point* p) {
    printf("(%d, %d)\n", p->x, p->y);
}

Node* create_node(int value) {
    Node* n = malloc(sizeof(Node));
    n->value = value;
    n->next = NULL;
    return n;
}

void push(Node** head, int value) {
    Node* n = create_node(value);
    n->next = *head;
    *head = n;
}

int main(int argc, char** argv) {
    Point p = {1, 2};
    print_point(&p);

    Node* list = NULL;
    push(&list, 10);
    push(&list, 20);
    return 0;
}
