import { Component, EventEmitter, Input, Output } from '@angular/core';

import {
  ProductEnumValue,
  ProductHandlerOutput,
  ProductRunDetail,
} from '../../product/product-api.models';
import { ProductRequestState } from '../../product/product-api-state';
import { formatProductEnumLabel, EnumKind } from '../operator-ui-format';
import { DetailTab } from '../operator-ui-tabs';
import { OperatorRunBatchesTabComponent } from './operator-run-batches-tab.component';
import { OperatorRunCapacityTabComponent } from './operator-run-capacity-tab.component';
import { OperatorRunDiagnosticsTabComponent } from './operator-run-diagnostics-tab.component';
import { OperatorRunHandlersTabComponent } from './operator-run-handlers-tab.component';
import { OperatorRunSourcesTabComponent } from './operator-run-sources-tab.component';
import { OperatorRunSummaryTabComponent } from './operator-run-summary-tab.component';

@Component({
  selector: 'app-operator-run-detail',
  imports: [
    OperatorRunBatchesTabComponent,
    OperatorRunCapacityTabComponent,
    OperatorRunDiagnosticsTabComponent,
    OperatorRunHandlersTabComponent,
    OperatorRunSourcesTabComponent,
    OperatorRunSummaryTabComponent,
  ],
  templateUrl: './operator-run-detail.component.html',
})
export class OperatorRunDetailComponent {
  @Input() selectedRunLoading = false;
  @Input() selectedRun: ProductRequestState<ProductRunDetail> | null = null;
  @Input() selectedRunId = '';
  @Input() activeTab: DetailTab = 'summary';
  @Input() handlerOutput: ProductRequestState<ProductHandlerOutput> | null = null;
  @Input() handlerOutputLoading = false;
  @Input() handlerLookupValidationMessage = '';
  @Input() handlerLookupDisabled = false;
  @Input() handlerSourceId = 0;
  @Input() handlerFieldName = '';

  @Output() activeTabChange = new EventEmitter<DetailTab>();
  @Output() handlerSourceIdChange = new EventEmitter<number>();
  @Output() handlerFieldNameChange = new EventEmitter<string>();
  @Output() loadHandlerOutput = new EventEmitter<void>();

  protected enumLabel(value: ProductEnumValue, kind: EnumKind): string {
    return formatProductEnumLabel(value, kind);
  }
}
