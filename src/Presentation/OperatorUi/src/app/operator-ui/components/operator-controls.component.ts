import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';

import {
  ProductControlSummary,
  ProductRunHistoryReadiness,
} from '../../product/product-api.models';
import { ProductRequestState } from '../../product/product-api-state';

@Component({
  selector: 'app-operator-controls',
  imports: [FormsModule],
  templateUrl: './operator-controls.component.html',
})
export class OperatorControlsComponent {
  @Input() controlOutcome: ProductRequestState<ProductControlSummary> | null = null;
  @Input() readiness: ProductRequestState<ProductRunHistoryReadiness> | null = null;
  @Input() controlTargetRunId = '';
  @Input() controlsDisabled = false;
  @Input() durableStorePath = '';

  @Output() durableStorePathChange = new EventEmitter<string>();
  @Output() stopAccepting = new EventEmitter<void>();
  @Output() drainAccepted = new EventEmitter<void>();
  @Output() cancelOpenAndRelease = new EventEmitter<void>();
  @Output() rejectUnsafeFallback = new EventEmitter<void>();
}
