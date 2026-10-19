import type { ReactNode } from 'react';
import type { WidgetSettings } from '../overlay/types';

type WidgetSlotProps = {
  settings: WidgetSettings;
  children: ReactNode;
};

export function WidgetSlot({ settings, children }: WidgetSlotProps) {
  return (
    <section
      className="widget-frame"
      style={{
        left: settings.left,
        top: settings.top,
        width: settings.width,
        height: settings.height,
      }}
    >
      {children}
    </section>
  );
}
