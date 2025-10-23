// src/app/shared/directives/lazy-observe.directive.ts
import { Directive, ElementRef, EventEmitter, Output, AfterViewInit, OnDestroy } from '@angular/core';

@Directive({
  selector: '[appLazyObserve]',
  standalone: true
})
export class LazyObserveDirective implements AfterViewInit, OnDestroy {
  @Output() visible = new EventEmitter<void>();
  private io?: IntersectionObserver;

  constructor(private el: ElementRef) {}

  ngAfterViewInit(): void {
    this.io = new IntersectionObserver((entries) => {
      for (const e of entries) {
        if (e.isIntersecting) {
          this.visible.emit();
          this.io?.disconnect(); // solo una vez
          this.io = undefined;
          break;
        }
      }
    }, { rootMargin: '200px' }); // precarga un poco antes
    this.io.observe(this.el.nativeElement);
  }

  ngOnDestroy(): void {
    this.io?.disconnect();
  }
}
