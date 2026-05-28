import { Component, EventEmitter, Input, Output } from '@angular/core';

import { ProductRunSummary } from '../../product/product-api.models';
import { ProductRequestState } from '../../product/product-api-state';
import { formatProductEnumLabel } from '../operator-ui-format';

@Component({
  selector: 'app-operator-run-list',
  templateUrl: './operator-run-list.component.html',
})
export class OperatorRunListComponent {
  @Input() runsLoading = false;
  @Input() runs: ProductRequestState<readonly ProductRunSummary[]> | null = null;
  @Input() selectedRunId = '';

  @Output() selectRun = new EventEmitter<string>();

  protected runCountLabel(): string {
    const count = this.runs?.body?.length;

    return count === undefined ? 'not loaded' : `${count} run${count === 1 ? '' : 's'}`;
  }

  protected stateLabel(state: ProductRunSummary['state']): string {
    return formatProductEnumLabel(state, 'state');
  }
}
